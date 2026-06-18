using System.Collections.Generic;
using UnityEngine;

namespace CwcdEsp.Data
{
    /// <summary>敌人缓存条目（值语义，绘制时只读）。</summary>
    public struct EnemyData
    {
        public int ActorId;
        public Vector3 Position;     // 脚底世界坐标
        public float Height;         // 身高（用于头顶方框）
        public float Radius;         // 碰撞半径（用于方框宽度）
        public Vector3 BoundsCenter; // 碰撞盒中心（Unity Collider.bounds.center）
        public Vector3 BoundsSize;   // 碰撞盒大小（Unity Collider.bounds.size）
        public int FractionValue;    // Fraction 枚举值（[Flags]）
        public bool Dead;
        public bool Visible;
        public float Hp;             // 当前血量（无可读时为 -1）
        public float MaxHp;          // 最大血量
        public string Name;          // 显示名

        public readonly bool HasHp => MaxHp > 0f && Hp >= 0f;
        public readonly float HpRatio => MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
        public readonly bool IsEnemy => (FractionValue & (2 | 4)) != 0; // Monster|Enemy
    }

    /// <summary>单个物资条目。</summary>
    public struct LootItem
    {
        public string Name;
        public int Rarity;   // 0~4
        public string Type;
        public int Count;
    }

    /// <summary>一个容器/掉落物的聚合缓存。</summary>
    public class LootEntry
    {
        public int ActorId;
        public Vector3 Position;
        public string ContainerName;
        public readonly List<LootItem> Items = new List<LootItem>(8);
    }

    /// <summary>追踪目标缓存（供子弹修正 O(1) 读取）。</summary>
    public class CachedTarget
    {
        public int ActorId = -1;
        public Vector3 Position;
        public bool IsValid;
        public float LastUpdate;

        public void Clear()
        {
            ActorId = -1;
            IsValid = false;
        }
    }
}
