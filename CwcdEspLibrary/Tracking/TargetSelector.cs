using CwcdEsp.Data;
using CwcdEsp.Esp;
using UnityEngine;

namespace CwcdEsp.Tracking
{
    /// <summary>
    /// 最近目标缓存（方案 6.4）。每帧主线程 Update 末尾运行一次，O(N)：
    /// 从 EnemyCache 读缓冲区筛选角度内最近候选 → 仅对最近候选做一次 Physics.Raycast（隔帧降频）→ 更新 CachedTarget。
    /// 子弹 Prefix 仅 O(1) 读取 CachedTarget，零遍历。
    /// </summary>
    public static class TargetSelector
    {
        /// <summary>子弹追踪读取的缓存目标。</summary>
        public static readonly CachedTarget CachedTarget = new CachedTarget();

        private static float _lastSightCheckTime = -1f;
        private static bool _lastSightVisible = true;

        /// <summary>主线程每帧调用（由 Patch_ActorUpdate.Postfix 触发）。</summary>
        public static void Update()
        {
            var enemies = EnemyCache.Instance.GetReadBuffer();
            Camera cam = ScreenTools.Cam;

            if (!EspConfig.BulletTrackingEnabled || cam == null || enemies == null || enemies.Count == 0)
            {
                CachedTarget.Clear();
                return;
            }

            Vector3 origin = cam.transform.position;
            Vector3 aim = cam.transform.forward;
            float maxDist = EspConfig.TrackingDistance;
            float maxAngle = EspConfig.TrackingAngle;

            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyData e = enemies[i];
                if (e.Dead) continue;

                Vector3 to = e.Position - origin;
                float distSq = to.sqrMagnitude;
                if (distSq > maxDist * maxDist) continue;

                float ang = Vector3.Angle(aim, to);
                if (ang > maxAngle) continue;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
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

            // 视线检测：降频（方案 6.4 / v3 修复 #16），只测最近候选
            bool visible = _lastSightVisible;
            if (Time.time - _lastSightCheckTime >= EspConfig.SightCheckInterval)
            {
                _lastSightCheckTime = Time.time;
                visible = CheckLineOfSight(origin, targetPos);
                _lastSightVisible = visible;
            }

            if (visible)
            {
                CachedTarget.ActorId = best.ActorId;
                CachedTarget.Position = targetPos;
                CachedTarget.IsValid = true;
                CachedTarget.LastUpdate = Time.time;
            }
            else
            {
                CachedTarget.Clear();
            }
        }

        /// <summary>直线是否被障碍遮挡（穿墙检测）。</summary>
        private static bool CheckLineOfSight(Vector3 from, Vector3 to)
        {
            float dist = Vector3.Distance(from, to);
            if (dist < 0.1f) return true;
            // 略微缩短终点避免命中目标自身碰撞体
            Vector3 dir = (to - from) / dist;
            Vector3 adjustedTo = to - dir * 0.5f;
            if (Physics.Linecast(from, adjustedTo, out RaycastHit hit))
            {
                // 命中点距离 < 目标距离 → 中途有遮挡
                return hit.distance >= dist - 0.6f;
            }
            return true;
        }

        /// <summary>目标是否仍存活（供 BulletTracker 校验，方案 6.4 存活验证）。</summary>
        public static bool IsTargetAlive()
        {
            if (!CachedTarget.IsValid) return false;
            // 读缓冲区中若已无该 Actor（死亡/移除）则视为不存活
            var enemies = EnemyCache.Instance.GetReadBuffer();
            if (enemies == null) return false;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].ActorId == CachedTarget.ActorId)
                    return !enemies[i].Dead;
            }
            return false;
        }
    }
}
