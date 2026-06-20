using CwcdEsp.Data;
using CwcdEsp.Esp;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Tracking
{
    /// <summary>
    /// 最近目标缓存（修复版：xz 平面距离 + 关闭视线检测）。
    ///
    /// 根因：游戏俯视角相机在 y≈25 高度，Physics.Linecast 从相机到地面敌人
    /// 容易被屋顶/树冠遮挡，导致视线检测失败 → CachedTarget.Clear()。
    /// ESP 是穿墙透视工具，子弹追踪也应支持穿墙，故关闭视线检测。
    /// </summary>
    public static class TargetSelector
    {
        public static readonly CachedTarget CachedTarget = new CachedTarget();

        private static readonly System.Collections.Generic.HashSet<int> _aliveSet = new System.Collections.Generic.HashSet<int>(64);

        public static void Update()
        {
            var enemies = EnemyCache.Instance.GetReadBuffer();
            Camera cam = ScreenTools.Cam;

            _aliveSet.Clear();

            if (!EspConfig.BulletTrackingEnabled || cam == null || enemies == null || enemies.Count == 0)
            {
                CachedTarget.Clear();
                return;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 originXz = new Vector3(camPos.x, 0f, camPos.z);

            Vector3 camFwd = cam.transform.forward;
            Vector3 aimXz = new Vector3(camFwd.x, 0f, camFwd.z);
            if (aimXz.sqrMagnitude < 0.001f) aimXz = Vector3.forward;
            aimXz.Normalize();

            float maxDist = EspConfig.TrackingDistance;
            float maxAngle = EspConfig.TrackingAngle;

            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyData e = enemies[i];
                if (e.Dead) continue;

                _aliveSet.Add(e.ActorId);

                Vector3 enemyXz = new Vector3(e.Position.x, 0f, e.Position.z);
                Vector3 toXz = enemyXz - originXz;
                float distSqXz = toXz.sqrMagnitude;
                if (distSqXz > maxDist * maxDist) continue;

                if (distSqXz < 0.01f) continue;
                Vector3 toDirXz = toXz.normalized;
                float ang = Vector3.Angle(aimXz, toDirXz);
                if (ang > maxAngle) continue;

                if (distSqXz < bestDistSq)
                {
                    bestDistSq = distSqXz;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                CachedTarget.Clear();
                return;
            }

            EnemyData best = enemies[bestIndex];
            Vector3 targetPos = best.Position;

            // 直接设置目标（关闭视线检测，ESP 穿墙追踪）
            CachedTarget.ActorId = best.ActorId;
            CachedTarget.Position = targetPos;
            CachedTarget.IsValid = true;
            CachedTarget.LastUpdate = Time.time;
        }

        public static bool IsTargetAlive()
        {
            if (!CachedTarget.IsValid) return false;
            return _aliveSet.Contains(CachedTarget.ActorId);
        }
    }
}
