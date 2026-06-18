using System.Collections.Generic;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using MorbidOptimism.Client;
using MorbidOptimism.Common;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 敌人列表缓存 —— 双缓冲 + 真正的指针交换（方案 6.5 / v3 修复 #11）。
    /// 写入永远在 _writeBuffer（主线程独占），读取永远在 _readBuffer（任意线程无竞争）。
    /// SwapBuffers() 在主线程 Update 末尾执行，O(1) 交换两个 List 引用。
    /// </summary>
    public class EnemyCache
    {
        public static readonly EnemyCache Instance = new EnemyCache();

        private List<EnemyData> _readBuffer = new List<EnemyData>(64);
        private List<EnemyData> _writeBuffer = new List<EnemyData>(64);

        // 阵营位掩码：Fraction.Monster(2) | Fraction.Enemy(4)
        private const int EnemyMask = 2 | 4;

        public void Init()
        {
            _readBuffer.Clear();
            _writeBuffer.Clear();
        }

        /// <summary>
        /// 主线程 Update Postfix 调用：遍历 ActorViewerManager.dicActors，
        /// 过滤敌对 + 存活 + 可见，做距离/视锥剔除后写入写缓冲区。
        /// </summary>
        public void Update(ActorViewerManager mgr)
        {
            if (mgr == null || mgr.dicActors == null) return;

            var write = _writeBuffer;
            write.Clear();

            Camera cam = ScreenTools.Cam;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxDist = EspConfig.EnemyMaxDistance;
            float maxDistSq = maxDist * maxDist;

            // dicActors: Dictionary<int, ActorViewer>
            var dic = mgr.dicActors;
            foreach (var kv in dic)
            {
                ActorViewer viewer = kv.Value;
                if (viewer == null) continue;
                ActorData actor = viewer.actor;
                if (actor == null) continue;
                if (actor.dead) continue;

                int frac = (int)actor.fraction;
                if ((frac & EnemyMask) == 0) continue; // 非敌对跳过

                // 可见性（LogicDataActorLife.visible）
                bool visible = actor.lifeData == null || actor.lifeData.visible;

                // 距离剔除
                Vector3 pos = actor.position;
                float distSq = (pos - camPos).sqrMagnitude;
                if (distSq > maxDistSq) continue;

                // 视锥剔除（有相机时）
                if (cam != null && !ScreenTools.IsInFrustum(cam, pos)) continue;

                EnemyData e = new EnemyData
                {
                    ActorId = actor.id,
                    Position = pos,
                    Height = 1.8f, // TODO: 从 StaticData_Actor.values["height"] 读取实际身高
                    FractionValue = frac,
                    Dead = false,
                    Visible = visible,
                    Name = string.IsNullOrEmpty(actor.label) ? actor.staticDataKey : actor.label,
                    Hp = -1f,
                    MaxHp = 0f,
                };

                // 血量（LogicDataActorLife.hp -> ModableValue.value / .maxValue）
                var life = actor.lifeData;
                if (life != null && life.hp != null)
                {
                    e.Hp = life.hp.value;
                    e.MaxHp = life.hp.maxValue;
                }

                write.Add(e);
            }
        }

        /// <summary>主线程 Update 末尾调用：交换读写缓冲区（真正指针交换）。</summary>
        public void SwapBuffers()
        {
            var temp = _readBuffer;
            _readBuffer = _writeBuffer;
            _writeBuffer = temp;

            // 调试：确认地址变化（方案 10.1）
            // EntryPoint.Log($"Swap: read={_readBuffer.Count} write={_writeBuffer.Count}");
        }

        /// <summary>任意线程读取（子弹追踪 / 绘制均调用此方法，O(1) 无遍历）。</summary>
        public List<EnemyData> GetReadBuffer() => _readBuffer;
    }
}
