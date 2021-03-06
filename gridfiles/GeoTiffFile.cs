using System; 
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;

namespace gridfiles
{
    public class GeoTiffFile : GridFile
    {
        public enum TiffOutputType
        {
            HORIZONTAL_OFFSET = 0,
            VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL = 1,
            VERTICAL_OFFSET_VERTICAL_TO_VERTICAL = 2,
            GEOCENTRIC_TRANSLATION = 3,
            VELOCITY = 4,
            DEFORMATION_MODEL = 5
        }
      
        public enum TiffOutputTypeshort
        {
            hoffset = TiffOutputType.HORIZONTAL_OFFSET,
            geoid = TiffOutputType.VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL,
            vsep = TiffOutputType.VERTICAL_OFFSET_VERTICAL_TO_VERTICAL,
            goffset = TiffOutputType.GEOCENTRIC_TRANSLATION,
            vel = TiffOutputType.VELOCITY,
            deform = TiffOutputType.DEFORMATION_MODEL
        }

        private GridParam _gridParam;

        private GtxFile _gtxFile;
        private Ct2File _ct2File;
        private CommonPointSet _cps;

        private GdalMetadata _gdalMetadata = new GdalMetadata();
        private const int byteDepth = 4;
        private int _tileSize = 0;
        private string _imageDescription = "";
        private const TiffTag GeoKeyDirectoryTag = (TiffTag)34735;
        private const TiffTag GeoDoubleParamsTag = (TiffTag)34736;
        private const TiffTag GeoAsciiParamsTag = (TiffTag)34737;
        private const TiffTag TIFFTAG_ASCIITAG = (TiffTag)666;
        private const TiffTag GDAL_METADATA = (TiffTag)42112;
        private const TiffTag GDAL_NODATA = (TiffTag)42113;

        public GeoTiffFile()
        {
            _gridParam = new GridParam();

            _gtxFile = new GtxFile(_gridParam);
            _ct2File = new Ct2File(_gridParam);
            _cps = new CommonPointSet(_gridParam);          
        }

        public GeoTiffFile(string griEFilename, string griNFilename, string griUFilename)
        {
            _gridParam = new GridParam();

            _gtxFile = new GtxFile(griUFilename, _gridParam);
            _ct2File = new Ct2File(griNFilename, griEFilename, _gridParam);
            _cps = new CommonPointSet(_gridParam);
        }

        public GtxFile Gtx
        {
            get => _gtxFile;
            set => _gtxFile = value;
        }

        public Ct2File Ct2
        {
            get => _ct2File;
            set => _ct2File = value;
        }

        public CommonPointSet CommonPoints
        {
            get => _cps;
            set => _cps = value;
        }

        public TiffOutputType TiffOutput { get; set; }       

        public int Dimensions { get; set; } = 1;

        public CrsCode Epsg2d { get; set; } = new CrsCode();

        public CrsCode Epsg3d { get; set; } = new CrsCode();

        public CrsCode EpsgSource { get; set; } = new CrsCode();

        public CrsCode EpsgTarget { get; set; } = new CrsCode();

        public string Area_of_use { get; set; } = "";

        public string Grid_name { get; set; } = "";

        public double LowerLeftLatitude
        {
            get => _gridParam.LowerLeftLatitude;
            set => _gridParam.LowerLeftLatitude = value;
        }

        public double LowerLeftLongitude
        {
            get => _gridParam.LowerLeftLongitude;
            set => _gridParam.LowerLeftLongitude = value;
        }

        public double UpperLeftLatitude
        {
            get => _gridParam.UpperLeftLatitude;
        }

        public double UpperLeftLongitude
        {
            get => _gridParam.UpperLeftLongitude;
        }

        public double DeltaLatitude
        {
            get => _gridParam.DeltaLatitude;
            set => _gridParam.DeltaLatitude = value;
        }

        public double DeltaLongitude
        {
            get => _gridParam.DeltaLongitude;
            set => _gridParam.DeltaLongitude = value;
        }

        public Int32 NRows
        {
            get => _gridParam.NRows;
            set => _gridParam.NRows = value;
        }
        
        public Int32 NColumns
        {
            get => _gridParam.NColumns;
            set => _gridParam.NColumns = value;
        }

        public int TileSize
        {
            get => _tileSize;
            set => _tileSize = value;
        }

        public int Size => _tileSize * _tileSize;

        public string ImageDescription
        {
            get =>  _imageDescription + _cps.HelmertResult + _cps.LscParameters;
            set => _imageDescription = value;
        }

        public string Email { get; set; }

        internal UInt16[] GeoTags
        {
            get
            {
                var index = 0;
                UInt16 count = 2;

                count += (Epsg2d.CodeNumber > 0) ? (UInt16)1 : (UInt16)0;
                count += (Epsg3d.CodeNumber > 0) ? (UInt16)1 : (UInt16)0;

                var geotags = new UInt16[(count + 1) * 4];

                geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)count, index++);
                geotags.SetValue((UInt16)1024, index++); geotags.SetValue((UInt16)0, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)2, index++);
                geotags.SetValue((UInt16)1025, index++); geotags.SetValue((UInt16)0, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)2, index++);

                if (Epsg2d.CodeNumber > 0)
                {
                    geotags.SetValue((UInt16)2048, index++); geotags.SetValue((UInt16)0, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)Epsg2d.CodeNumber, index++);         
                }
                 
                if (Epsg3d.CodeNumber > 0)
                {
                    geotags.SetValue((UInt16)4096, index++); geotags.SetValue((UInt16)0, index++); geotags.SetValue((UInt16)1, index++); geotags.SetValue((UInt16)Epsg3d.CodeNumber, index++);                
                }                  
                return geotags; 
            }
        }           

        public bool ReadCptFile(string inputFileName)
        {
            if (Dimensions == 1 || Dimensions == 3)
            {
                if (!_gtxFile.CptFile.ReadCptFile(inputFileName))
                    return false;
            }
            if (Dimensions == 3 || Dimensions == 2)
            {
                if (!_ct2File.CptFile.ReadCptFile(inputFileName))
                    return false;
            }
            return true;
        }
        
        public bool ReadGriFiles()
        {
            if (Dimensions > 1)
            {
                if (!_ct2File.GriEast.ReadGridFile())
                    return false;

                if (!_ct2File.GriNorth.ReadGridFile())
                    return false;
            }
            if (Dimensions != 2)
                if (!_gtxFile.GriHeight.ReadGridFile())
                    return false;
 
            return true;
        }

        public override bool PopulatedGrid(double k, double c, double sn)
        {
            if (Dimensions == 1)
                return _gtxFile.PopulatedGrid(k, c, sn);

            if (Dimensions == 2)
                return _ct2File.PopulateGrid(k, c, sn);

            if (Dimensions == 3)
                return _cps.PopulatedGrid(k, c, sn);

            return false;
        }

        public override bool ReadSourceFromFile(string inputFileName)
        {
            if (Dimensions == 1)
                return _gtxFile.ReadSourceFromFile(inputFileName);

            if (Dimensions == 2)
                return _ct2File.ReadSourceFromFile(inputFileName);

            if (Dimensions == 3)
                return _cps.ReadSourceFromFile(inputFileName);

            return false;
        }

        public override bool ReadTargetFromFile(string inputFileName)
        {
            if (Dimensions == 1)
                return _gtxFile.ReadTargetFromFile(inputFileName);

            if (Dimensions == 2)
                return _ct2File.ReadTargetFromFile(inputFileName);

            if (Dimensions == 3)
                return _cps.ReadTargetFromFile(inputFileName);

            return false;
        }

        public void CleanNullPoints()
        {
            _cps.CleanNullPoints();
            _ct2File.CleanNullPoints();
            _gtxFile.CleanNullPoints();
        }

        public bool GenerateMetadata(Tiff tiff)
        {
            // Note: Tile size has to be divided by 16.
            if (TileSize == 0 || TileSize % 16 != 0)
                return false;

            TagExtender(tiff);

            if (!tiff.SetField(TiffTag.DOCUMENTNAME, Path.GetFileName(OutputFileName)))
                return false;
            
            if (!tiff.SetField(TiffTag.DATETIME, DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss")))
                return false;
            
            if (!tiff.SetField(TiffTag.IMAGEWIDTH, NColumns))
                return false;

            if (!tiff.SetField(TiffTag.IMAGELENGTH, NRows))
                return false;

            if (!tiff.SetField(TiffTag.SAMPLESPERPIXEL, Dimensions))
                return false;
            
            if (Dimensions > 1)
            {
                short[] a = new short[Dimensions - 1];

                for (int i = 0; i < a.Length; i++)
                    a[i] = (short)ExtraSample.UNSPECIFIED;

                if (!tiff.SetField(TiffTag.EXTRASAMPLES, Dimensions - 1, a))
                    return false;
            }

            if (!tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK))
                return false;

            if (!tiff.SetField(TiffTag.PLANARCONFIG, Dimensions > 1 ? PlanarConfig.SEPARATE : PlanarConfig.CONTIG))
                return false;

            if (!tiff.SetField(TiffTag.BITSPERSAMPLE, 8 * byteDepth))
                return false;

            // TODO: Is probably not needed
            /*
            if (!tiff.SetField(TiffTag.ROWSPERSTRIP, NRows))
                return false;
            */

            if (!tiff.SetField(TiffTag.COMPRESSION, Compression.ADOBE_DEFLATE))
                return false;

            if (!tiff.SetField(TiffTag.PREDICTOR, Predictor.FLOATINGPOINT))
                return false;

            if (!tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB))
                return false;

            if (!tiff.SetField(TiffTag.MAKE, "Kartverket (Norwegian Mapping Authority)"))
                return false;

            if (!tiff.SetField(TiffTag.COPYRIGHT, "Kartverket (Norwegian Mapping Authority). Creative Commons Attribution 4.0 https://creativecommons.org/licenses/by/4.0/"))
                return false;

            if (!tiff.SetField(TiffTag.SOFTWARE, $"{Assembly.GetEntryAssembly().GetName().Name}, version {Assembly.GetEntryAssembly().GetName().Version}" ))
                return false;            
         
            if (!tiff.SetField(TiffTag.IMAGEDESCRIPTION, ImageDescription))
                return false;

            if (!tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.IEEEFP))
                return false;

            if (!tiff.SetField(TiffTag.TILEWIDTH, TileSize))
                return false;

            if (!tiff.SetField(TiffTag.TILELENGTH, TileSize))
                return false;

            if (!tiff.SetField(TiffTag.ARTIST, Email))
                return false;

            double[] modelpixelscaletag = { DeltaLongitude, DeltaLatitude, 0.0 };
            if (!tiff.SetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG, modelpixelscaletag))
                return false;

            double[] geotiff_modeltiepointtag = { 0d, 0d, 0d, UpperLeftLongitude, UpperLeftLatitude, 0d };
            if (!tiff.SetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG, geotiff_modeltiepointtag))
                return false;
 
            if (!tiff.SetField(GeoKeyDirectoryTag, GeoTags))
                return false;

            _gdalMetadata.Clear();
           
            _gdalMetadata.AddItem(new Item() { Name = Item.NameType.area_of_use, MyString = Area_of_use });
            _gdalMetadata.AddItem(new Item() { Name = Item.NameType.grid_name, MyString = Grid_name });

            if (EpsgSource.AutorityName != "")
            {
                if (EpsgSource.AutorityName.ToUpper() == "EPSG")
                {
                    if (EpsgSource.CodeNumber > 0)
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.source_crs_epsg_code, MyString = EpsgSource.CodeNumber.ToString() });
                }
                else
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.source_crs_wkt, MyString = EpsgSource.GetWktString() });
            }
            if (EpsgTarget.AutorityName != "")
            {
                if (EpsgTarget.AutorityName.ToUpper() == "EPSG")
                {
                    if (EpsgTarget.CodeNumber > 0)
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.target_crs_epsg_code, MyString = EpsgTarget.CodeNumber.ToString() });
                }
                else
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.target_crs_wkt, MyString = EpsgTarget.GetWktString() });
            }
               
            _gdalMetadata.AddItem(new Item() { Name = Item.NameType.TYPE, MyString = TiffOutput.ToString() });

            int bandNo = 0; // band no.

            if (TiffOutput == TiffOutputType.VELOCITY)
            {
                if (Dimensions == 3 || Dimensions == 2)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "millimetres per year" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "east_velocity" });

                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "millimetres per year" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "north_velocity" });
                }
                if (Dimensions == 3 || Dimensions == 1)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "millimetres per year" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "up_velocity" });
                 }
            }
            else if (TiffOutput == TiffOutputType.HORIZONTAL_OFFSET)
            {
                if (Dimensions == 2)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "radian" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "latitude_offset" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.positive_value, Sample = bandNo.ToString(), MyString = "west" }); // "west" has the same sign as ct2

                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "radian" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "longitude_offset" });
                }
            }
            else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_VERTICAL_TO_VERTICAL)
            {
                if (Dimensions == 1)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "vertical_offset" });
                }
            }
            else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL)
            {
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "geoid_undulation" });
            }
            else if (TiffOutput == TiffOutputType.GEOCENTRIC_TRANSLATION)
            {
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "x_translation" });

                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "y_translation" });

                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "z_translation" });
            }

            if (!tiff.SetField(GDAL_NODATA, Int16.MinValue))
                return false;

            string xmlValue = GdalMetadata.SerializeToString(_gdalMetadata);
            if (!tiff.SetField(GDAL_METADATA, xmlValue))
                 return false;

            if (!tiff.CheckpointDirectory())
                return false;

            return true;
        }

        /*
        * References:
        * https://portal.dgiwg.org/files/?artifact_id=68102&format=pdf    
        * http://geotiff.maptools.org/spec/geotiff3.html
        * http://geotiff.maptools.org/spec/geotiff6.html
        * https://www.alternatiff.com/resources/TIFF6.pdf
        * https://fossies.org/diffs/libgeotiff/1.4.2_vs_1.4.3/csv/coordinate_reference_system.csv-diff.html
        * https://docs.opengeospatial.org/is/19-008r4/19-008r4.html
        *
        * // For GDAL_METADATA:
        * https://www.awaresystems.be/imaging/tiff/tifftags/gdal_metadata.html 
        * https://gdal.org/drivers/raster/gtiff.html
        */
        public override bool GenerateGridFile(string outputFileName, bool isRandom = false)
        {
            try
            {
                OutputFileName = outputFileName;

                if (OutputFileName == "")
                    return false;

                using (var tiff = Tiff.Open(OutputFileName, "w"))
                {
                    if (tiff == null)
                        return false;

                    if (!GenerateMetadata(tiff))
                        return false;

                    if (TiffOutput == TiffOutputType.HORIZONTAL_OFFSET)
                    {
                        if (Dimensions == 3 || Dimensions == 2)
                        {
                            if (!WriteBand(tiff, _ct2File.GriNorth.Data))
                                return false;

                            if (!WriteBand(tiff, _ct2File.GriEast.Data))
                                return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_VERTICAL_TO_VERTICAL)
                    {
                        if (Dimensions == 3 || Dimensions == 1)
                        {
                            if (!WriteBand(tiff, _gtxFile.GriHeight.Data))
                                return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL)
                    {
                        if (Dimensions == 1)
                        {
                            if (!WriteBand(tiff, _gtxFile.GriHeight.Data))
                                return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VELOCITY)
                    {
                        if (Dimensions == 3 || Dimensions == 2)
                        {
                            if (!WriteBand(tiff, _ct2File.GriEast.Data))
                                return false;

                            if (!WriteBand(tiff, _ct2File.GriNorth.Data))
                                return false;
                        }
                        if (Dimensions == 3 || Dimensions == 1)
                        {
                            if (!WriteBand(tiff, _gtxFile.GriHeight.Data))
                                return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.GEOCENTRIC_TRANSLATION)
                    {
                        if (!WriteBand(tiff, _cps.GriX.Data))
                            return false;

                        if (!WriteBand(tiff, _cps.GriY.Data))
                            return false;

                        if (!WriteBand(tiff, _cps.GriZ.Data))
                            return false;
                    }

                    if (!tiff.WriteDirectory())
                        return false;

                    tiff.Close();
                }
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal bool WriteBand(Tiff tiff, List<float> bandList)
        {
            var myDictionary = BandListToTiledDictionary(bandList);           

            if (myDictionary is null)
                return false;

            var indexTile = tiff.CurrentTile() == 0 ? 0 : tiff.CurrentTile() + 1;

            foreach (var fArray in myDictionary.Values)
            {
                var byteArray = new Byte[Size * byteDepth];
                Buffer.BlockCopy(fArray, 0, byteArray, 0, byteArray.Length);

                if (tiff.WriteEncodedTile(indexTile++, byteArray, Size * byteDepth) == -1)
                    return false;
            }

            if (!tiff.CheckpointDirectory())
                return false;

            return true;
        }  

        internal Dictionary<KeyValuePair<int, int>, float[]> BandListToTiledDictionary(List<float> bandList)
        {
            if (bandList == null)
                return null;

            var myDictionary = new Dictionary<KeyValuePair<int, int>, float[]>();

            int indexTileRow = 0;
            int indexTileCol = 0;
            int indexRow = 0;
            int indexCol = 0;         
            int indexData = 0;
            float[] floatArray = new float[0];

            for (var row = 0; row < NRows; row++)
            {
                indexRow = row / TileSize;
                indexTileRow = row % TileSize;

                for (var col = 0; col < NColumns; col++)
                {
                    indexCol = col / TileSize;
                    indexTileCol = col % TileSize;

                    if (indexTileCol == 0 && indexTileRow == 0)
                        myDictionary.Add(new KeyValuePair<int, int>(indexRow, indexCol), new float[Size]);

                    if (myDictionary.TryGetValue(new KeyValuePair<int, int>(indexRow, indexCol), out floatArray))
                    {
                        var v = bandList[indexData++];

                        if (v == -88.8888f)
                            v = float.NaN;

                        // Logging av verdiar:
                        // Console.WriteLine($"indexRow: {indexRow} indexCol: {indexCol} indexTileRow: {indexTileRow} indexTileCol: {indexTileCol} indexData: {indexData - 1} value: {v}");

                        floatArray[indexTileCol + indexTileRow * TileSize + Size * 0] = v;
                    }
                    else
                        return null;
                }
            }
            return myDictionary;
        }

        public void TestGdalMetadata()
        {
            var gdaltest = new gridfiles.GdalMetadata();
            gdaltest.SerializeObject("GDAL_Metadata.xml");

            var item = GdalMetadata.SerializeToString(gdaltest);
        }

        internal void TagExtender(Tiff tif)
        {         
            TiffFieldInfo[] tiffFieldInfo =
            {
                new TiffFieldInfo(TiffTag.GEOTIFF_MODELPIXELSCALETAG, 3, 3, TiffType.DOUBLE, 65, true, false, "GEOTIFF_MODELPIXELSCALETAG"),
                new TiffFieldInfo(TiffTag.GEOTIFF_MODELTIEPOINTTAG, 6, 6, TiffType.DOUBLE, 65, true, false, "GEOTIFF_MODELTIEPOINTTAG"),
                new TiffFieldInfo(TiffTag.GEOTIFF_MODELTRANSFORMATIONTAG, 16, 16, TiffType.DOUBLE, FieldBit.Custom, true, false, "GEOTIFF_MODELTRANSFORMATIONTAG"),
                new TiffFieldInfo(GeoKeyDirectoryTag, (short)GeoTags.Length, (short)GeoTags.Length, TiffType.SHORT, FieldBit.Custom, true, false, "GeoKeyDirectoryTag"),
                new TiffFieldInfo(GDAL_METADATA, -1, -1, TiffType.ASCII, FieldBit.Custom, true, false, "GDAL_METADATA"),
                new TiffFieldInfo(GDAL_NODATA, -1, -1,  TiffType.ASCII,  FieldBit.Custom, true, false, "GDAL_NODATA")
            };
            tif.MergeFieldInfo(tiffFieldInfo, tiffFieldInfo.Length); 
        }
    }
}
