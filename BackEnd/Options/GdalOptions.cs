namespace BackEnd.Options;

public class GdalOptions
{
    public bool Enabled { get; set; } = false;
    public string Ogr2OgrPath { get; set; } = "ogr2ogr";
    public string GdalRasterizePath { get; set; } = "gdal_rasterize";
    public string Gdal2TilesPath { get; set; } = "gdal2tiles";
    public string WorkingTempPath { get; set; } = "./_data/temp";
    public int OutputWidth { get; set; } = 4096;
    public int MinZoom { get; set; } = 0;
    public int MaxZoom { get; set; } = 4;
    public int TimeoutSeconds { get; set; } = 120;
}
