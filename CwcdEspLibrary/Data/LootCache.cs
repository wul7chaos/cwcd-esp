using System.Collections.Generic;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 物资列表缓存 —— 逐 Actor 脏标记（方案 5.1 / v3 修复 #14）。
    /// 仅当某 Actor 的容器发生变动（PutItem/_RemoveItem）时，标记该 Actor 为脏，
    /// UpdateDirtyActors 只扫描脏 Actor，避免全局全量扫描造成的帧率波动。
    /// </summary>
    public class LootCache
    {
        public static readonly LootCache Instance = new LootCache();

        // actorId → 是否需要刷新
        private readonly Dictionary<int, bool> _actorDirty = new Dictionary<int, bool>(128);
        // actorId → 物品列表缓存
        private readonly Dictionary<int, LootEntry> _loot = new Dictionary<int, LootEntry>(128);
        // 绘制用快照（主线程刷新，OnGUI 读取）
        private readonly List<LootEntry> _snapshot = new List<LootEntry>(128);

        public void Init()
        {
            _actorDirty.Clear();
            _loot.Clear();
            _snapshot.Clear();
        }

        /// <summary>容器变动时调用：标记该 Actor 为脏（细化到单个 Actor，非全局）。</summary>
        public void MarkDirty(int actorId)
        {
            if (actorId <= 0) return;
            _actorDirty[actorId] = true;
        }

        /// <summary>
        /// 主线程 Update Postfix 调用：仅扫描脏 Actor 的容器，刷新后清除脏标记。
        /// </summary>
        public void UpdateDirtyActors()
        {
            if (_actorDirty.Count == 0) return;

            // 收集脏 ActorId（边遍历边改字典不安全，先转列表）
            var dirtyIds = new List<int>(_actorDirty.Count);
            foreach (var kv in _actorDirty)
            {
                if (kv.Value) dirtyIds.Add(kv.Key);
            }

            foreach (int actorId in dirtyIds)
            {
                // TODO: 通过 actorId 定位 ActorData + 其 LogicDataActorItems/LogicDataActorPickable 容器，
                //       调用 ScanContainer 扫描物品并写入 _loot[actorId]。
                //       数据路径见方案 5.1/5.2：
                //         item.data.staticDataKey → Singleton<StaticDataManager>.GetInstance().item[key]
                //           → name / rarity(0~4) / type
                //       此处为基础骨架，具体扫描逻辑在 LootScanner（待实现）中补全。
                ScanContainer(actorId);
                _actorDirty[actorId] = false;
            }
        }

        /// <summary>扫描单个 Actor 的容器，更新缓存条目（骨架）。</summary>
        private void ScanContainer(int actorId)
        {
            // 占位：实际实现需读取游戏容器数据。
            // 保留缓存条目，避免绘制空引用。
            if (!_loot.ContainsKey(actorId))
            {
                _loot[actorId] = new LootEntry { ActorId = actorId };
            }
        }

        /// <summary>主线程 Update 末尾：重建绘制快照（含距离/视锥/稀有度剔除）。</summary>
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
                // 稀有度过滤：至少有一个物品达到最小稀有度才显示
                if (!HasMinRarity(entry)) continue;
                _snapshot.Add(entry);
            }
        }

        private bool HasMinRarity(LootEntry entry)
        {
            if (EspConfig.MinRarity <= 0) return true;
            for (int i = 0; i < entry.Items.Count; i++)
            {
                if (entry.Items[i].Rarity >= EspConfig.MinRarity) return true;
            }
            return false;
        }

        /// <summary>OnGUI 绘制时读取。</summary>
        public List<LootEntry> GetSnapshot() => _snapshot;

        /// <summary>供 LootScanner 写入位置（容器世界坐标）。</summary>
        public void SetLootPosition(int actorId, Vector3 pos)
        {
            if (!_loot.TryGetValue(actorId, out var entry))
            {
                entry = new LootEntry { ActorId = actorId };
                _loot[actorId] = entry;
            }
            entry.Position = pos;
        }
    }
}
