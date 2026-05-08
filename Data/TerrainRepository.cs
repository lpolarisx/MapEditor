using System.Collections.Generic;
using System.Drawing;
using MapEditor.Models;

namespace MapEditor.Data
{
    /// <summary>
    /// 地形数据仓库，数据来自 Terrain.json（TerrainDetails）
    /// ID 0-10，共 11 条
    /// </summary>
    public static class TerrainRepository
    {
        // MapColor 是 UI 叠加色，按地形语义手动配置
        private static readonly List<TerrainData> _terrains = new List<TerrainData>
        {
            new TerrainData
            {
                ID = 0, 
                Name = "未知",
                CanExtendInto = false, 
                ViewThrough = false, 
                GraphicLayer = 0,
                FireDamageRate = 1f, 
                FoodDeposit = 0, 
                FoodRegainDays = 0,
                FoodSpringRate = 0, 
                FoodSummerRate = 0, 
                FoodAutumnRate = 0, 
                FoodWinterRate = 0,
                RoutewayActiveFundCost = 10000, 
                RoutewayBuildFundCost = 10000,
                RoutewayBuildWorkCost = 10000, 
                RoutewayConsumptionRate = 1f,
                MapColor = Color.FromArgb(70, 80, 80, 80)
            },
            new TerrainData
            {
                ID = 1, 
                Name = "平原",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 30,
                FireDamageRate = 1f, 
                FoodDeposit = 100000, 
                FoodRegainDays = 60,
                FoodSpringRate = 0.6f, 
                FoodSummerRate = 0.9f, 
                FoodAutumnRate = 1.2f, 
                FoodWinterRate = 0.3f,
                RoutewayActiveFundCost = 4, 
                RoutewayBuildFundCost = 30,
                RoutewayBuildWorkCost = 40, 
                RoutewayConsumptionRate = 0.008f,
                MapColor = Color.FromArgb(70, 120, 210, 90)
            },
            new TerrainData
            {
                ID = 2, Name = "草原",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 40,
                FireDamageRate = 0.8f, 
                FoodDeposit = 50000, 
                FoodRegainDays = 90,
                FoodSpringRate = 0.5f, 
                FoodSummerRate = 1f, 
                FoodAutumnRate = 0.8f, 
                FoodWinterRate = 0.2f,
                RoutewayActiveFundCost = 6, 
                RoutewayBuildFundCost = 40,
                RoutewayBuildWorkCost = 50, 
                RoutewayConsumptionRate = 0.01f,
                MapColor = Color.FromArgb(70, 80, 185, 80)
            },
            new TerrainData
            {
                ID = 3, 
                Name = "森林",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 50,
                FireDamageRate = 1.5f, 
                FoodDeposit = 80000, 
                FoodRegainDays = 30,
                FoodSpringRate = 0.8f, 
                FoodSummerRate = 1.1f, 
                FoodAutumnRate = 0.9f, 
                FoodWinterRate = 0.5f,
                RoutewayActiveFundCost = 8, 
                RoutewayBuildFundCost = 60,
                RoutewayBuildWorkCost = 60, 
                RoutewayConsumptionRate = 0.012f,
                MapColor = Color.FromArgb(70, 34, 120, 34)
            },
            new TerrainData
            {
                ID = 4, 
                Name = "湿地",
                CanExtendInto = false, 
                ViewThrough = true, 
                GraphicLayer = 20,
                FireDamageRate = 1f, 
                FoodDeposit = 40000, 
                FoodRegainDays = 75,
                FoodSpringRate = 0.7f, 
                FoodSummerRate = 1.2f, 
                FoodAutumnRate = 1f, 
                FoodWinterRate = 0.3f,
                RoutewayActiveFundCost = 25, 
                RoutewayBuildFundCost = 200,
                RoutewayBuildWorkCost = 500, 
                RoutewayConsumptionRate = 0.03f,
                MapColor = Color.FromArgb(70, 60, 160, 140)
            },
            new TerrainData
            {
                ID = 5, 
                Name = "山地",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 80,
                FireDamageRate = 1.2f, 
                FoodDeposit = 60000, 
                FoodRegainDays = 45,
                FoodSpringRate = 0.7f, 
                FoodSummerRate = 1f, 
                FoodAutumnRate = 1.1f, 
                FoodWinterRate = 0.4f,
                RoutewayActiveFundCost = 10, 
                RoutewayBuildFundCost = 90,
                RoutewayBuildWorkCost = 100, 
                RoutewayConsumptionRate = 0.016f,
                MapColor = Color.FromArgb(70, 140, 130, 120)
            },
            new TerrainData
            {
                ID = 6, 
                Name = "水域",
                CanExtendInto = false, 
                ViewThrough = true, 
                GraphicLayer = 10,
                FireDamageRate = 1f, 
                FoodDeposit = 30000, 
                FoodRegainDays = 15,
                FoodSpringRate = 0.5f, 
                FoodSummerRate = 1f, 
                FoodAutumnRate = 1f, 
                FoodWinterRate = 0.2f,
                RoutewayActiveFundCost = 2, 
                RoutewayBuildFundCost = 30,
                RoutewayBuildWorkCost = 30, 
                RoutewayConsumptionRate = 0.005f,
                MapColor = Color.FromArgb(70, 30, 120, 255)
            },
            new TerrainData
            {
                ID = 7, 
                Name = "峻岭",
                CanExtendInto = false, 
                ViewThrough = false, 
                GraphicLayer = 100,
                FireDamageRate = 1f, 
                FoodDeposit = 0, 
                FoodRegainDays = 0,
                FoodSpringRate = 0, 
                FoodSummerRate = 0, 
                FoodAutumnRate = 0, 
                FoodWinterRate = 0,
                RoutewayActiveFundCost = 10000, 
                RoutewayBuildFundCost = 10000,
                RoutewayBuildWorkCost = 10000, 
                RoutewayConsumptionRate = 1f,
                MapColor = Color.FromArgb(70, 90, 80, 75)
            },
            new TerrainData
            {
                ID = 8, 
                Name = "荒地",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 60,
                FireDamageRate = 1f, 
                FoodDeposit = 10000, 
                FoodRegainDays = 120,
                FoodSpringRate = 0.6f, 
                FoodSummerRate = 1f, 
                FoodAutumnRate = 0.8f, 
                FoodWinterRate = 0.1f,
                RoutewayActiveFundCost = 9, 
                RoutewayBuildFundCost = 80,
                RoutewayBuildWorkCost = 60, 
                RoutewayConsumptionRate = 0.015f,
                MapColor = Color.FromArgb(70, 190, 160, 100)
            },
            new TerrainData
            {
                ID = 9, 
                Name = "栈道",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 70,
                FireDamageRate = 1f, 
                FoodDeposit = 5000, 
                FoodRegainDays = 180,
                FoodSpringRate = 0.7f, 
                FoodSummerRate = 1f, 
                FoodAutumnRate = 0.9f, 
                FoodWinterRate = 0.2f,
                RoutewayActiveFundCost = 8, 
                RoutewayBuildFundCost = 75,
                RoutewayBuildWorkCost = 80, 
                RoutewayConsumptionRate = 0.012f,
                MapColor = Color.FromArgb(70, 160, 100, 50)
            },
            new TerrainData
            {
                ID = 10, 
                Name = "瘴气林",
                CanExtendInto = true, 
                ViewThrough = true, 
                GraphicLayer = 90,
                FireDamageRate = 1.6f, 
                FoodDeposit = 5000, 
                FoodRegainDays = 180,
                FoodSpringRate = 0.3f, 
                FoodSummerRate = 0.5f, 
                FoodAutumnRate = 0.4f, 
                FoodWinterRate = 0.1f,
                RoutewayActiveFundCost = 16, 
                RoutewayBuildFundCost = 150,
                RoutewayBuildWorkCost = 100, 
                RoutewayConsumptionRate = 0.024f,
                MapColor = Color.FromArgb(70, 60, 100, 40)
            },
        };

        public static IReadOnlyList<TerrainData> All => _terrains.AsReadOnly();

        public static TerrainData GetById(int id)
        {
            return _terrains.Find(t => t.ID == id);
        }
    }
}
