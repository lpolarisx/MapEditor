using System.Drawing;

namespace MapEditor.Models
{
    /// <summary>
    /// 地形数据，字段与 Terrain.json 一致
    /// </summary>
    public class TerrainData
    {
        public int ID { get; set; }
        public string Name { get; set; }

        // 视线 / 图形层次
        public bool ViewThrough { get; set; }
        public int GraphicLayer { get; set; }

        // 粮道
        public int RoutewayBuildFundCost { get; set; }
        public int RoutewayActiveFundCost { get; set; }
        public int RoutewayBuildWorkCost { get; set; }
        public float RoutewayConsumptionRate { get; set; }

        // 粮草
        public int FoodDeposit { get; set; }
        public int FoodRegainDays { get; set; }
        public float FoodSpringRate { get; set; }
        public float FoodSummerRate { get; set; }
        public float FoodAutumnRate { get; set; }
        public float FoodWinterRate { get; set; }

        // 火焰伤害 / 可扩展
        public float FireDamageRate { get; set; }
        public bool CanExtendInto { get; set; }

        // UI 用：地形叠加色（不在 JSON 里，本地配置）
        public Color MapColor { get; set; }

        public override string ToString() => $"[{ID:D2}] {Name}";
    }
}
