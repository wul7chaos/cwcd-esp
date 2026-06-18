using System.Collections.Generic;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using MorbidOptimism.Client;
using MorbidOptimism.Common;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 敌人列表缓存 —— 双缓冲 + 真正的指针交换。
    /// 修复版：使用 ActorViewer.transform.position（Unity 渲染坐标）而非 ActorData.position（逻辑坐标），
    /// 并读取 Collider.bounds 获取精确碰撞盒。
    /// </summary>
    public class EnemyCache
    {
        public static readonly EnemyCache Instance = new EnemyCache();

        private List<EnemyData> _readBuffer = new List<EnemyData>(64);
        private List<EnemyData> _writeBuffer = new List<EnemyData>(64);

        private const int EnemyMask = 2 | 4;

        // 日志限频
        private static float _lastLogTime = 0f;
        private static int _logCount = 0;

        public void Init()
        {
            _readBuffer.Clear();
            _writeBuffer.Clear();
        }

        public void Update(ActorViewerManager mgr)
        {
            if (mgr == null || mgr.dicActors == null) return;

            var write = _writeBuffer;
            write.Clear();

            Camera cam = ScreenTools.Cam;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxDist = EspConfig.EnemyMaxDistance;
            float maxDistSq = maxDist * maxDist;

            var dic = mgr.dicActors;
            foreach (var kv in dic)
            {
                ActorViewer viewer = kv.Value;
                if (viewer == null) continue;
                ActorData actor = viewer.actor;
                if (actor == null) continue;
                if (actor.dead) continue;

                int frac = (int)actor.fraction;
                if ((frac & EnemyMask) == 0) continue;

                bool visible = actor.lifeData == null || actor.lifeData.visible;

                // 关键修复：优先用 viewer.transform.position（Unity 渲染坐标），
                // 因为 ActorData.position 是服务器逻辑坐标，可能与客户端渲染位置有偏差
                Vector3 pos = viewer.transform != null ? viewer.transform.position : actor.position;

                float distSq = (pos - camPos).sqrMagnitude;
                if (distSq > maxDistSq) continue;

                if (cam != null && !ScreenTools.IsInFrustum(cam, pos)) continue;

                // 读取碰撞盒
                Vector3 boundsCenter = pos;
                Vector3 boundsSize = Vector3.zero;
                float radius = 0.5f;
                float height = 1.8f;

                if (viewer.actorCollider != null)
                {
                    Bounds bounds = viewer.actorCollider.bounds;
                    boundsCenter = bounds.center;
                    boundsSize = bounds.size;
                    radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                    height = bounds.size.y;
                }

                // 也尝试从 colliderData 获取 radius
                if (actor.colliderData != null && actor.colliderData.radius > 0.01f)
                {
                    radius = Mathf.Max(radius, actor.colliderData.radius);
                }

                EnemyData e = new EnemyData
                {
                    ActorId = actor.id,
                    Position = pos,
                    Height = height > 0.1f ? height : 1.8f,
                    Radius = radius,
                    BoundsCenter = boundsCenter,
                    BoundsSize = boundsSize,
                    FractionValue = frac,
                    Dead = false,
                    Visible = visible,
                    Name = string.IsNullOrEmpty(actor.label) ? actor.staticDataKey : actor.label,
                    Hp = -1f,
                    MaxHp = 0f,
                };

                var life = actor.lifeData;
                if (life != null && life.hp != null)
                {
                    e.Hp = life.hp.value;
                    e.MaxHp = life.hp.maxValue;
                }

                write.Add(e);
            }

            // 限频日志（前3次 + 每10秒一次）
            if ((_logCount < 3 || Time.time - _lastLogTime > 10f) && write.Count > 0)
            {
                _lastLogTime = Time.time;
                _logCount++;
                var first = write[0];
                FileLogger.Info($"[EnemyCache] 缓存 {write.Count} 敌人。首个: pos={first.Position} boundsCenter={first.BoundsCenter} radius={first.Radius} height={first.Height}");
            }
        }

        public void SwapBuffers()
        {
            var temp = _readBuffer;
            _readBuffer = _writeBuffer;
            _writeBuffer = temp;
        }

        public List<EnemyData> GetReadBuffer() => _readBuffer;
    }
}
