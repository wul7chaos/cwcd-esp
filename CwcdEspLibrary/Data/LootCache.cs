using System.Collections.Generic;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using MorbidOptimism.Client;
using MorbidOptimism.Common;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 物资列表缓存（修复版：使用 IItemContainer 接口泛化扫描所有容器类型）。
    ///
    /// 根因分析：游戏的可搜索容器（蘑菇/厕所/木头/文档/尸体）使用 LogicDataActorSearchPoint，
    /// 而非 LogicDataActorItems 或 LogicDataActorPickable。前者有 container 字段（ItemContainer_Grid）。
    /// 所有容器类型都实现 IItemContainer 接口，通过 GetItemEnumerator() 获取物品。
    ///
    /// 实现 IItemContainer 的 LogicData 类型：
    ///   - LogicDataActorSearchPoint (可搜索点：蘑菇/厕所/木头/文档/尸体)
    ///   - LogicDataActorItems (NPC 物品：背包/胸挂/装备)
    ///   - LogicDataActorPickable (地上掉落物)
    ///   - LogicDataActorPortalToilet (传送厕所)
    ///   - LogicDataActorTrader (商人)
    ///
    /// 物品信息路径：
    ///   IItemContainer.GetItemEnumerator() → Item
    ///     → item.data.staticDataKey (string)
    ///     → item.data.staticData.name (本地化名称)
    ///     → item.data.staticData.rarity (int 0~4)
    ///     → item.data.num (堆叠数量)
    /// </summary>
    public class LootCache
    {
        public static readonly LootCache Instance = new LootCache();

        private readonly Dictionary<int, LootEntry> _loot = new Dictionary<int, LootEntry>(128);
        private readonly List<LootEntry> _snapshot = new List<LootEntry>(128);

        private int _lastFoundCount = -1;

        public void Init()
        {
            _loot.Clear();
            _snapshot.Clear();
        }

        /// <summary>脏标记（保留接口兼容，全量扫描模式下为空操作）。</summary>
        public void MarkDirty(int actorId) { /* no-op: 全量扫描模式不需要脏标记 */ }

        /// <summary>
        /// 主线程每帧调用：扫描所有 Actor，通过 IItemContainer 接口找到有物品的容器。
        /// </summary>
        public void ScanAllActors(ActorViewerManager mgr)
        {
            if (mgr == null || mgr.dicActors == null) return;

            _loot.Clear();

            int foundCount = 0;
            var dic = mgr.dicActors;
            foreach (var kv in dic)
            {
                ActorViewer viewer = kv.Value;
                if (viewer == null) continue;
                ActorData actor = viewer.actor;
                if (actor == null) continue;
                if (actor.dead) continue;

                // 遍历所有 logicDatas，查找实现 IItemContainer 的
                IItemContainer itemContainer = null;
                string containerTypeName = null;
                if (actor.logicDatas != null)
                {
                    for (int i = 0; i < actor.logicDatas.Count; i++)
                    {
                        var ld = actor.logicDatas[i];
                        if (ld is IItemContainer ic)
                        {
                            itemContainer = ic;
                            containerTypeName = ld.GetType().Name;
                            break;
                        }
                    }
                }

                if (itemContainer == null) continue;

                // 用 viewer.transform.position（渲染坐标）
                Vector3 pos = viewer.transform != null ? viewer.transform.position : actor.position;

                // 容器名称：根据类型给出友好名
                string containerName = GetContainerName(containerTypeName, actor);

                LootEntry entry = new LootEntry
                {
                    ActorId = actor.id,
                    Position = pos,
                    ContainerName = containerName,
                };

                // 通过 IItemContainer 接口枚举所有物品
                try
                {
                    var enumerator = itemContainer.GetItemEnumerator();
                    if (enumerator != null)
                    {
                        foreach (var item in enumerator)
                        {
                            AddItem(item, entry);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    FileLogger.Warn($"[LootCache] 枚举物品失败 actor={actor.id} type={containerTypeName}: {ex.Message}");
                }

                if (entry.Items.Count > 0)
                {
                    _loot[actor.id] = entry;
                    foundCount++;
                }
            }

            // 仅在物品数量变化时记录
            if (foundCount != _lastFoundCount)
            {
                _lastFoundCount = foundCount;
                FileLogger.Info($"[LootCache] 扫描完成: 有物品={foundCount}");
            }
        }

        /// <summary>根据 LogicData 类型名生成友好容器名。</summary>
        private static string GetContainerName(string typeName, ActorData actor)
        {
            if (typeName == null) return "容器";
            switch (typeName)
            {
                case "LogicDataActorSearchPoint": return "可搜索";
                case "LogicDataActorItems": return "掉落";
                case "LogicDataActorPickable": return "掉落物";
                case "LogicDataActorPortalToilet": return "厕所";
                case "LogicDataActorTrader": return "商人";
                default: return typeName.Replace("LogicDataActor", "");
            }
        }

        /// <summary>从 Item 提取名称/稀有度/数量/价值。</summary>
        private void AddItem(Item item, LootEntry entry)
        {
            if (item == null || item.data == null) return;
            try
            {
                string name = item.data.staticDataKey ?? "???";
                int rarity = 0;
                int count = 1;
                int buyPrice = 0;

                // 尝试获取 staticData
                var sd = item.data.staticData;
                if (sd != null)
                {
                    name = sd.name;
                    rarity = sd.rarity;
                }

                // 堆叠数量
                count = item.data.num > 1 ? item.data.num : 1;

                // 购买价（含子物品叠加，ItemData_Equip 会重写此方法累加 subItems 价值）
                // 签名：ItemDataBase.GetBuyPrice(int num, int currency = 1)
                try
                {
                    buyPrice = item.data.GetBuyPrice(item.data.num);
                }
                catch
                {
                    // 某些特殊物品可能无法计算价格，保持 0
                    buyPrice = 0;
                }

                entry.Items.Add(new LootItem
                {
                    Name = name,
                    Rarity = rarity,
                    Count = count,
                    BuyPrice = buyPrice,
                });
            }
            catch
            {
                entry.Items.Add(new LootItem
                {
                    Name = item.data.staticDataKey ?? "???",
                    Rarity = 0,
                    Count = 1,
                    BuyPrice = 0,
                });
            }
        }

        /// <summary>主线程 Update 末尾：重建绘制快照（含距离/稀有度/价值剔除 + 价值排序 + 数量限制）。</summary>
        /// <remarks>
        /// 过滤粒度=物品级：每个物品独立按稀有度+价值过滤，只把通过过滤的物品放入 FilteredItems。
        /// 然后按 BuyPrice 降序排序，若 MaxLootItemsPerContainer > 0 则截取前N个。
        /// 容器内全部物品都不通过 → 该容器不加入 snapshot。
        /// </remarks>
        public void RebuildSnapshot()
        {
            _snapshot.Clear();
            Camera cam = ScreenTools.Cam;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxDistSq = EspConfig.LootMaxDistance * EspConfig.LootMaxDistance;

            bool valueFilterOn = EspConfig.EnableLootFilter;
            int minValue = EspConfig.MinItemValue;
            int minRarity = EspConfig.MinRarity;
            int maxItems = EspConfig.MaxLootItemsPerContainer;

            foreach (var kv in _loot)
            {
                LootEntry entry = kv.Value;
                // 距离剔除（用 xz 平面距离，忽略相机高度差）
                if (cam != null)
                {
                    float dx = entry.Position.x - camPos.x;
                    float dz = entry.Position.z - camPos.z;
                    float distSqXz = dx * dx + dz * dz;
                    if (distSqXz > maxDistSq) continue;
                }

                // 物品级过滤：逐物品判断是否满足稀有度+价值条件
                entry.FilteredItems.Clear();
                for (int i = 0; i < entry.Items.Count; i++)
                {
                    LootItem li = entry.Items[i];
                    if (li.Rarity < minRarity) continue;
                    if (valueFilterOn && li.BuyPrice < minValue) continue;
                    entry.FilteredItems.Add(li);
                }

                // 容器内至少有一件物品通过过滤才显示
                if (entry.FilteredItems.Count == 0) continue;

                // 按价值降序排序（价值高的排前面）
                entry.FilteredItems.Sort((a, b) => b.BuyPrice.CompareTo(a.BuyPrice));

                // 数量限制：只保留前 maxItems 个（已按价值降序）
                if (maxItems > 0 && entry.FilteredItems.Count > maxItems)
                {
                    entry.FilteredItems.RemoveRange(maxItems, entry.FilteredItems.Count - maxItems);
                }

                _snapshot.Add(entry);
            }
        }

        public List<LootEntry> GetSnapshot() => _snapshot;
    }
}
