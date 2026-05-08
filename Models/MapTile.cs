namespace MapEditor.Models
{
    public class MapTile
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public int TileImageIndex { get; set; }    // 对应 tile 图片编号 (0-899)
        public int TerrainId { get; set; }         // 绑定的地形 ID
        public int[,] TerrainIds { get; set; }     // tile 内部 10×10 地形 ID

        // 是否被选中（高亮）
        public bool IsSelected { get; set; }
    }
}
