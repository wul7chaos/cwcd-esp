using System.Collections.Generic;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using MorbidOptimism.Client;
using MorbidOptimism.Common;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 物资列表缓存（修复版：每帧扫描所有 Actor 获取物资位置和物品信息）。
    ///
    /// 数据路径：
    ///   ActorViewerManager.dicActors → ActorViewer.actor → ActorData
    ///     → GetLogicData&lt;LogicDataActorItems&gt;() → backpack/chestHanging (ItemContainer_Grid)
    ///       → items (List&lt;Item&gt;) → item.data.staticDataKey → StaticData_Item.name/rarity
    ///     → GetLogicData&lt;LogicDataActorPickable&gt;() → container (ItemContainer_Single)
    ///       → item.data.staticDataKey → StaticData_Item.name/rarity
    /// </summary>
    public class LootCache
    {
        public static readonly LootCache Instance = new LootCache();

        private readonly Dictionary<int, bool> _actorDirty = new Dictionary<int, bool>(128);
        private readonly Dictionary<int, LootEntry> _loot = new Dictionary<int, LootEntry>(128);
        private readonly List<LootEntry> _snapshot = new List<LootEntry>(128);

        // 日志限频
        private static int _scanLogCount = 0;
        private static float _lastScanLogTime = 0f;

        public void Init()
        {
            _actorDirty.Clear();
            _loot.Clear();
            _snapshot.Clear();
        }

        public void MarkDirty(int actorId)
        {
            if (actorId <= 0) return;
            _actorDirty[actorId] = true;
        }

        /// <summary>
        /// 主线程每帧调用：扫描所有 Actor，找到有物品容器的 Actor 并记录位置和物品。
        /// 注：不再依赖脏标记（脏标记只用于触发增量更新），每帧全量扫描位置保证位置准确。
        /// </summary>
        public void ScanAllActors(ActorViewerManager mgr)
        {
            if (mgr == null || mgr.dicActors == null) return;

            // 清空旧数据（位置每帧更新，物品也每帧重建以确保准确）
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

                // 检查是否有物品逻辑数据
                var items = actor.GetLogicData<LogicDataActorItems>();
                var pickable = actor.GetLogicData<LogicDataActorPickable>();

                if (items == null && pickable == null) continue;

                LootEntry entry = new LootEntry
                {
                    ActorId = actor.id,
                    Position = actor.position,
                    ContainerName = pickable != null ? "掉落物" : "容器",
                };

                // 扫描 LogicDataActorItems 的容器
                if (items != null)
                {
                    ScanGridContainer(items.backpack, entry);
                    ScanGridContainer(items.chestHanging, entry);
                }

                // 扫描 LogicDataActorPickable 的容器
                if (pickable != null && pickable.container != null)
                {
                    ScanSingleContainer(pickable.container, entry);
                }

                if (entry.Items.Count > 0)
                {
                    _loot[actor.id] = entry;
                    foundCount++;
                }
            }

            // 限频日志（每5秒最多一次）
            if (Time.time - _lastScanLogTime > 5f && _scanLogCount < 3)
            {
                _lastScanLogTime = Time.time;
                _scanLogCount++;
                FileLogger.Info($"[LootCache] 扫描完成: 找到 {foundCount} 个有物品的容器");
            }
        }

        private void ScanGridContainer(ItemContainer_Grid grid, LootEntry entry)
        {
            if (grid == null || grid.items == null) return;
            for (int i = 0; i < grid.items.Count; i++)
            {
                AddItem(grid.items[i], entry);
            }
        }

        private void ScanSingleContainer(ItemContainer_Single container, LootEntry entry)
        {
            // ItemContainer_Single 应该有 item 字段或类似
            // 从反编译源码看，它继承自 IItemContainer
            // 尝试用反射获取 item
            try
            {
                var itemField = container.GetType().GetField("item");
                if (itemField != null)
                {
                    var item = itemField.GetValue(container) as Item;
                    if (item != null) AddItem(item, entry);
                    return;
                }
                // 备选：尝试 GetItemEnumerator
                var enumMethod = container.GetType().GetMethod("GetItemEnumerator");
                if (enumMethod != null)
                {
                    var enumerator = enumMethod.Invoke(container, null) as IEnumerable<Item>;
                    if (enumerator != null)
                    {
                        foreach (var item in enumerator)
                        {
                            AddItem(item, entry);
                        }
                    }
                }
            }
            catch { /* 反射失败忽略 */ }
        }

        private void AddItem(Item item, LootEntry entry)
        {
            if (item == null || item.data == null) return;
            try
            {
                string name = item.data.staticDataKey;
                int rarity = 0;
                // 尝试获取 staticData（可能抛异常如果 StaticDataManager 未初始化）
                var sd = item.data.staticData;
                if (sd != null)
                {
                    name = sd.name;
                    rarity = sd.rarity;
                }
                entry.Items.Add(new LootItem
                {
                    Name = name,
                    Rarity = rarity,
                    Count = 1,
                });
            }
            catch
            {
                // StaticDataManager 可能未就绪，用 staticDataKey 兜底
                entry.Items.Add(new LootItem
                {
                    Name = item.data.staticDataKey ?? "???",
                    Rarity = 0,
                    Count = 1,
                });
            }
        }

        /// <summary>脏标记刷新（保留接口兼容，实际扫描在 ScanAllActors 中完成）。</summary>
        public void UpdateDirtyActors()
        {
            // 脏标记清空（ScanAllActors 已全量扫描）
            if (_actorDirty.Count > 0) _actorDirty.Clear();
        }

        /// <summary>主线程 Update 末尾：重建绘制快照（含距离/稀有度剔除）。</summary>
        public void RebuildSnapshot()
        {
            _snapshot.Clear();
            Camera cam = ScreenTools.Cam;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxDistSq = EspConfig.LootMaxDistance * EspConfig.LootMaxDistance;

            foreach (var kv in _loot)
            {
                LootEntry entry = kv.Value;
                // 距离剔除
                if (cam != null && (entry.Position - camPos).sqrMagnitude > maxDistSq) continue;
                _snapshot.Add(entry);
            }
        }

        public List<LootEntry> GetSnapshot() => _snapshot;
    }
}
