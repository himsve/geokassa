﻿using System; 
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using static gridfiles.Item;

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

        public enum RasterTypeEnum
        {
            RasterPixelIsArea = 1,
            RasterPixelIsPoint = 2
        }

        public enum ModelTypeEnum
        {            
            ModelTypeProjected = 1,
            ModelTypeGeographic = 2
        }

        private GridParam _gridParam;

        private GtxFile _gtxFile;
        private GtxFile _gtxSdFile;
        private Ct2File _ct2File;
        private Ct2File _ct2SdFile;
        private CommonPointSet _cps;
        private VelocityFile _velocityFile;

        private GDALMetadata _gdalMetadata = new GDALMetadata();
        private const int byteDepth = 4;
        private int _tileSize = 0;
        private int _tileCount = 0;
        private string _imageDescription = "";
        private const TiffTag GeoKeyDirectoryTag = (TiffTag)34735;
        private const TiffTag GeoDoubleParamsTag = (TiffTag)34736;
        private const TiffTag GeoAsciiParamsTag = (TiffTag)34737;
        private const TiffTag TIFFTAG_ASCIITAG = (TiffTag)666;
        private const TiffTag GDAL_METADATA = (TiffTag)42112;
        private const TiffTag GDAL_NODATA = (TiffTag)42113;
        private byte[] _data;

        public GeoTiffFile()
        {
            _gridParam = new GridParam();
             
            _velocityFile = new VelocityFile(_gridParam);
        }

        public GeoTiffFile(string griEFilename, string griNFilename, string griUFilename)
        {
            _gridParam = new GridParam();

            _gtxFile = new GtxFile(griUFilename, _gridParam);
            _ct2File = new Ct2File(griNFilename, griEFilename, _gridParam);
        }

        public GtxFile Gtx
        {
            get => _gtxFile = _gtxFile ?? new GtxFile(_gridParam);
            set => _gtxFile = value;
        }

        public GtxFile GtxSdFile
        {
            get => _gtxSdFile = _gtxSdFile ?? new GtxFile(_gridParam);
            set => _gtxSdFile = value;
        }         

        public Ct2File Ct2
        {
            get => _ct2File = _ct2File ?? new Ct2File(_gridParam);
            set => _ct2File = value;
        }

        public Ct2File Ct2SdFile
        {
            get => _ct2SdFile = _ct2SdFile ?? new Ct2File(_gridParam);
            set => _ct2SdFile = value;
        }

        public CommonPointSet CommonPoints
        {
            get => _cps = _cps ?? new CommonPointSet(_gridParam);
            set => _cps = value;
        }

        public new CommonPointList CommonPointList
        {
            get => CommonPoints.CommonPointList;
            set => CommonPoints.CommonPointList = value;
        }

        public TiffOutputType TiffOutput { get; set; }

        public int Dimensions { get; set; } = 1;

        public int AccuracyBands
        {
            get
            {
                return (GtxSdFile.Data.Any() ? 1 : 0) +
                    (Ct2SdFile.GriEast.Data.Any() ? 1 : 0) +
                    (Ct2SdFile.GriNorth.Data.Any() ? 1 : 0);
            }
        }

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

        public double UpperLeftLatitude => _gridParam.UpperLeftLatitude;
        
        public double UpperLeftLongitude => _gridParam.UpperLeftLongitude;

        public double UpperRightLatitude => _gridParam.UpperRightLatitude;

        public double UpperRightLongitude => _gridParam.UpperRightLongitude;

        public double LowerRightLatitude => _gridParam.LowerRightLatitude;

        public double LowerRightLongitude => _gridParam.LowerRightLongitude;

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

        public int TileCount
        {
            get => _tileCount;
            set => _tileCount = value;
        }

        public int Size => _tileSize * _tileSize;

        public int Samples { get; set; }

        public int BitsPerSample { get; set; }

        public int Bytes => BitsPerSample / 8;

        public string ImageDescription
        {
            get => _imageDescription + CommonPoints.HelmertResult + CommonPoints.LscParameters;
            set => _imageDescription = value;
        }

        public RasterTypeEnum RasterType { get; set; } = RasterTypeEnum.RasterPixelIsPoint;

        public ModelTypeEnum ModelType { get; set; } = ModelTypeEnum.ModelTypeGeographic;

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

                geotags.SetValue((UInt16)1, index++);
                geotags.SetValue((UInt16)1, index++); 
                geotags.SetValue((UInt16)1, index++);
                geotags.SetValue((UInt16)count, index++);

                geotags.SetValue((UInt16)1024, index++); 
                geotags.SetValue((UInt16)0, index++);
                geotags.SetValue((UInt16)1, index++);
                geotags.SetValue((UInt16)ModelType, index++);

                geotags.SetValue((UInt16)1025, index++);
                geotags.SetValue((UInt16)0, index++);
                geotags.SetValue((UInt16)1, index++);
                geotags.SetValue((UInt16)RasterType, index++);

                if (Epsg2d.CodeNumber > 0)
                {
                    geotags.SetValue((UInt16)2048, index++);
                    geotags.SetValue((UInt16)0, index++);
                    geotags.SetValue((UInt16)1, index++);
                    geotags.SetValue((UInt16)Epsg2d.CodeNumber, index++);
                }
                if (Epsg3d.CodeNumber > 0)
                {
                    geotags.SetValue((UInt16)4096, index++);
                    geotags.SetValue((UInt16)0, index++);
                    geotags.SetValue((UInt16)1, index++);
                    geotags.SetValue((UInt16)Epsg3d.CodeNumber, index++);
                }                  
                return geotags; 
            }
        }           

        public bool ReadCptFile(string inputFileName)
        {
            if (Dimensions == 1 || Dimensions == 3)
            {
                if (!Gtx.CptFile.ReadCptFile(inputFileName))
                    return false;
            }
            if (Dimensions == 3 || Dimensions == 2)
            {
                if (!Ct2.CptFile.ReadCptFile(inputFileName))
                    return false;
            }
            return true;
        }
        
        public bool ReadGriFiles()
        {
            if (Dimensions > 1)
            {
                if (!Ct2.GriEast.ReadGridFile())
                    return false;

                if (!Ct2.GriNorth.ReadGridFile())
                    return false;
            }
            if (Dimensions != 2)
                if (!Gtx.GriHeight.ReadGridFile())
                    return false;
 
            return true;
        }

        public bool ReadVelocityFile(string inputFileName)
        {
            if (!File.Exists(inputFileName))
                return false;

            if (Dimensions == 3)
            {
                if (!_velocityFile.ReadVelocityFile(inputFileName))
                    return false;

                Console.WriteLine($"NRows: {_velocityFile.NRows}");
                Console.WriteLine($"NColumns: {_velocityFile.NColumns}");
                Console.WriteLine($"DeltaLatitude: {_velocityFile.DeltaLatitude}");
                Console.WriteLine($"DeltaLongitude: {_velocityFile.DeltaLongitude}");
                Console.WriteLine($"LowerLeftLatitude: {_velocityFile.LowerLeftLatitude}");
                Console.WriteLine($"LowerLeftLongitude: {_velocityFile.LowerLeftLongitude}");
                Console.WriteLine($"LowerRightLatitude: {_velocityFile.LowerRightLatitude}");
                Console.WriteLine($"LowerRightLongitude: {_velocityFile.LowerRightLongitude}");
                Console.WriteLine($"UpperLeftLatitude: {_velocityFile.UpperLeftLatitude}");
                Console.WriteLine($"UpperLeftLongitude: {_velocityFile.UpperLeftLongitude}");
                Console.WriteLine($"UpperRightLatitude: {_velocityFile.UpperRightLatitude}");
                Console.WriteLine($"UpperRightLongitude: {_velocityFile.UpperRightLongitude}");
            }
            return true;
        }

        public override bool PopulatedGrid(double k, double c, double sn)
        {
            if (Dimensions == 1)
                return Gtx.PopulatedGrid(k, c, sn);

            if (Dimensions == 2)
                return Ct2.PopulateGrid(k, c, sn);

            if (Dimensions == 3)
                return CommonPoints.PopulatedGrid(k, c, sn);

            return false;
        }

        public override bool ReadSourceFromFile(string inputFileName)
        {
            if (Dimensions == 1)
                return Gtx.ReadSourceFromFile(inputFileName);

            if (Dimensions == 2)
                return Ct2.ReadSourceFromFile(inputFileName);

            if (Dimensions == 3)
                return CommonPoints.ReadSourceFromFile(inputFileName);

            return false;
        }

        public override bool ReadTargetFromFile(string inputFileName)
        {
            if (Dimensions == 1)
                return Gtx.ReadTargetFromFile(inputFileName);

            if (Dimensions == 2)
                return Ct2.ReadTargetFromFile(inputFileName);

            if (Dimensions == 3)
                return CommonPoints.ReadTargetFromFile(inputFileName);

            return false;
        }

        public bool ReadGeoTiff(string inputTiffFile)
        {
            if (!File.Exists(inputTiffFile))
                return false;

            if (!(_data == null))
                return true;

            using (var tiff = Tiff.Open(inputTiffFile, "r"))
            {
                NRows = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                NColumns = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();

                FieldValue[] modelPixelScaleTag = tiff.GetField((TiffTag)33550);
                FieldValue[] modelTiepointTag = tiff.GetField((TiffTag)33922);

                byte[] modelPixelScale = modelPixelScaleTag[1].GetBytes();
                double pixelSizeX = BitConverter.ToDouble(modelPixelScale, 0);
                double pixelSizeY = BitConverter.ToDouble(modelPixelScale, 8)*-1;

                DeltaLongitude = pixelSizeX;
                DeltaLatitude = -pixelSizeY;

                byte[] modelTransformation = modelTiepointTag[1].GetBytes();
                double originLon = BitConverter.ToDouble(modelTransformation, 24);
                double originLat = BitConverter.ToDouble(modelTransformation, 32);

                LowerLeftLongitude = originLon;
                LowerLeftLatitude = -DeltaLatitude * (NRows - 1) + originLat;

                BitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
                Samples = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
                                
                ImageDescription = tiff.GetField(TiffTag.IMAGEDESCRIPTION).Count() > 0 ? tiff.GetField(TiffTag.IMAGEDESCRIPTION)[0].ToString() : "";

                Email = tiff.GetField(TiffTag.ARTIST).Count() > 0 ? tiff.GetField(TiffTag.ARTIST)[0].ToString() : "";

                var geoKeyDirectoryTag = tiff.GetField(GeoKeyDirectoryTag);

                ReadGeoTiffTags(geoKeyDirectoryTag);

                var metadata = tiff.GetField(GDAL_METADATA);

                var gdalMetadata = SerializedObject<GDALMetadata>.StringToSerialize(metadata);
                
                if (gdalMetadata == null)
                    gdalMetadata = SerializedObject<GdalMetadata>.StringToSerialize(metadata);
                
                if (gdalMetadata == null)
                    return false;
                
                if (gdalMetadata.GdalMetadataList.Any(x => x.Name == NameType.area_of_use))
                    Area_of_use = gdalMetadata.GdalMetadataList.Find(x => x.Name == NameType.area_of_use).MyString;

                if (gdalMetadata.GdalMetadataList.Any(x => x.Name == NameType.grid_name))
                    Grid_name = gdalMetadata.GdalMetadataList.Find(x => x.Name == NameType.grid_name).MyString;

                if (gdalMetadata.GdalMetadataList.Any(x => x.Name == NameType.TYPE))
                    TiffOutput = ParseEnum<TiffOutputType>(gdalMetadata.GdalMetadataList.Find(x => x.Name == NameType.TYPE).MyString);

                if (gdalMetadata.GdalMetadataList.Any(x => x.Name == NameType.source_crs_epsg_code))
                    EpsgSource.CodeString = "EPSG:" + gdalMetadata.GdalMetadataList.Find(x => x.Name == NameType.source_crs_epsg_code).MyString;

                if (gdalMetadata.GdalMetadataList.Any(x => x.Name == NameType.target_crs_epsg_code))
                    EpsgTarget.CodeString = "EPSG:" + gdalMetadata.GdalMetadataList.Find(x => x.Name == NameType.target_crs_epsg_code).MyString;

                if (tiff.IsTiled())
                {
                    TileSize = tiff.TileSize();
 
                    var tileRowSize = tiff.TileRowSize();
                    var numberOfTiles = tiff.NumberOfTiles();
                    var tileBuffer = new byte[TileSize];
                    var size = (int)(tileRowSize / Bytes);

                    var colSize = size;
                    var rowSize = size;

                    int noOfTiledCol = (int)Math.Ceiling((double)NColumns / (double)size);

                    int noOfTiledRowFloor = (int) NRows / size;
                    int noOfTiledColFloor = (int) NColumns / size;

                    for (int k = 0; k < numberOfTiles; k++)
                    {
                        var res = tiff.ReadEncodedTile(k, tileBuffer, 0, TileSize);

                        if (res == -1)
                        {
                            tiff.Close();
                            return false;
                        }

                        var byteArray = new Byte[Bytes];
                        
                        var rowTile = k / noOfTiledCol;
                        var colTile = k % noOfTiledCol;

                        if (colTile == noOfTiledColFloor)
                            colSize = NColumns - colTile * size;

                        if (rowTile == noOfTiledRowFloor)
                            rowSize = NRows - rowTile * size;

                        for (int i = 0; i < rowSize; i++)
                        {
                            for (int j = 0; j < colSize; j++)
                            {                                
                                Buffer.BlockCopy(tileBuffer, j * Bytes + i * tileRowSize, byteArray, 0, byteArray.Length);

                                var value = BitConverter.ToSingle(byteArray, 0);
                                
                                var arrayIndex = j + i * NColumns + rowTile * size * NColumns + colTile * size;

                                while (arrayIndex >= Gtx.Data.Count())
                                    Gtx.Data.Add(0f);

                                Gtx.Data[arrayIndex] = value;
                            }
                        }
                        colSize = size;
                        rowSize = size;
                    }
                }
                else
                {
                    _data = new byte[NRows * NColumns * Bytes * Samples];
        
                    var scanline = new byte[tiff.ScanlineSize()];
                    
                    for (int k = 0; k < Samples; k++)
                    {
                        for (int i = 0; i < NRows; i++)
                        {
                            tiff.ReadScanline(scanline, i, (short)k);
                            Buffer.BlockCopy(scanline, 0, _data, scanline.Length * (NRows - i - 1) + k * scanline.Length * NRows, scanline.Length);
                        }
                    }
                }
                tiff.Close();
            }
            return true;
        }

        public bool GetGeoTiffValue(double latitude, double longitude, out object[] value)
        {
            value = new object[Samples];

            if (_data == null)
                return false;

            if (!IsInSideExtent(latitude, longitude))
                return false;

            var gridLat = (latitude - LowerLeftLatitude) / DeltaLatitude;
            var gridLon = (longitude - LowerLeftLongitude) / DeltaLongitude;

            int lat1 = (int)Math.Floor(gridLat);
            int lon1 = (int)Math.Floor(gridLon);

            int lat2 = lat1;
            int lon2 = lon1 + 1;

            int lat3 = lat1 + 1;
            int lon3 = lon1;

            int lat4 = lat1 + 1;
            int lon4 = lon1 + 1;

            var dx1 = GetValue(NColumns * lat1 + lon1, 0);
            var dx2 = GetValue(NColumns * lat2 + lon2, 0);
            var dx3 = GetValue(NColumns * lat3 + lon3, 0);
            var dx4 = GetValue(NColumns * lat4 + lon4, 0);

            var dy1 = GetValue(NColumns * lat1 + lon1, 1);
            var dy2 = GetValue(NColumns * lat2 + lon2, 1);
            var dy3 = GetValue(NColumns * lat3 + lon3, 1);
            var dy4 = GetValue(NColumns * lat4 + lon4, 1);

            var dz1 = GetValue(NColumns * lat1 + lon1, 2);
            var dz2 = GetValue(NColumns * lat2 + lon2, 2);
            var dz3 = GetValue(NColumns * lat3 + lon3, 2);
            var dz4 = GetValue(NColumns * lat4 + lon4, 2);

            double frctLon = gridLon - lon1;
            double frctLat = gridLat - lat1;
            double m10 = frctLon;
            double m11 = m10;
            double m01 = 1d - frctLon;
            double m00 = m01;

            m11 *= frctLat;
            m01 *= frctLat;
            frctLat = 1d - frctLat;
            m00 *= frctLat;
            m10 *= frctLat;

            var v1 = m00 * dx1 + m10 * dx2 + m01 * dx3 + m11 * dx4;
            var v2 = m00 * dy1 + m10 * dy2 + m01 * dy3 + m11 * dy4;
            var v3 = m00 * dz1 + m10 * dz2 + m01 * dz3 + m11 * dz4;

            value[0] = v1;
            value[1] = v2;
            value[2] = v3;

            return true;
        }

        internal double GetValue(int index, int band)
        {
            var byteArray = new Byte[Bytes];
            Buffer.BlockCopy(_data, (index + band * NRows * NColumns) * Bytes, byteArray, 0, byteArray.Length);
            var value = BitConverter.ToSingle(byteArray);

            return value;
        }

        internal bool IsInSideExtent(double latitude, double longitude)
        {
            return latitude > LowerLeftLatitude && latitude < UpperLeftLatitude && longitude > LowerLeftLongitude && longitude < UpperRightLongitude; 
        }

        public void CleanNullPoints()
        {
            CommonPoints.CleanNullPoints();
            Ct2.CleanNullPoints();
            Gtx.CleanNullPoints();
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

            if (!tiff.SetField(TiffTag.SAMPLESPERPIXEL, Dimensions + AccuracyBands))
                return false;
            
            if (Dimensions + AccuracyBands > 1)
            {
                short[] a = new short[Dimensions + AccuracyBands - 1];

                for (int i = 0; i < a.Length; i++)
                    a[i] = (short)ExtraSample.UNSPECIFIED;

                if (!tiff.SetField(TiffTag.EXTRASAMPLES, Dimensions + AccuracyBands - 1, a))
                    return false;
            }

            if (!tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK))
                return false;

            if (!tiff.SetField(TiffTag.PLANARCONFIG, Dimensions + AccuracyBands > 1 ? PlanarConfig.SEPARATE : PlanarConfig.CONTIG))
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
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.positive_value, Sample = bandNo.ToString(), MyString = "west" }); // "west" has same sign as ct2

                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "radian" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "longitude_offset" });

                    if (Ct2SdFile.GriNorth.Data.Any() && Ct2SdFile.GriEast.Data.Any())
                    {
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "radian" });
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "latitude_offset_accuracy" });

                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "radian" });
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "longitude_offset_accuracy" });
                    }
                }
            }
            else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_VERTICAL_TO_VERTICAL)
            {
                if (Dimensions == 1)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "vertical_offset" });

                    if (GtxSdFile.Data.Any())
                    {
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                        _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "vertical_offset_accuracy" });
                    }
                }
            }
            else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL)
            {
                if (Dimensions == 1)
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "geoid_undulation" });
                }
                if (GtxSdFile.Data.Any())
                {
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.UNITTYPE, Sample = bandNo.ToString(), MyString = "metre" });
                    _gdalMetadata.AddItem(new Item() { Name = Item.NameType.DESCRIPTION, Sample = (bandNo++).ToString(), MyString = "vertical_offset_accuracy" });
                }
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

            string xmlValue = SerializedObject<GDALMetadata>.SerializeToString(_gdalMetadata);
          
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
                            if (!WriteBand(tiff, Ct2.GriNorth.Data))
                                return false;

                            if (!WriteBand(tiff, Ct2.GriEast.Data))
                                return false;

                            if (Ct2SdFile.GriNorth.Data.Any() && Ct2SdFile.GriEast.Data.Any())
                            {
                                if (!WriteBand(tiff, Ct2SdFile.GriNorth.Data))
                                    return false;

                                if (!WriteBand(tiff, Ct2SdFile.GriEast.Data))
                                    return false;
                            }
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_VERTICAL_TO_VERTICAL)
                    {
                        if (Dimensions == 3 || Dimensions == 1)
                        {
                            if (!WriteBand(tiff, Gtx.GriHeight.Data))
                                return false;

                            if (GtxSdFile.GriHeight.Data.Any())
                                if (!WriteBand(tiff, GtxSdFile.GriHeight.Data))
                                    return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VERTICAL_OFFSET_GEOGRAPHIC_TO_VERTICAL)
                    {
                        if (Dimensions == 1)
                        {
                            if (!WriteBand(tiff, Gtx.GriHeight.Data))
                                return false;

                            if (GtxSdFile.GriHeight.Data.Any())
                                if (!WriteBand(tiff, GtxSdFile.GriHeight.Data))
                                    return false;
                        }
                    }
                    else if (TiffOutput == TiffOutputType.VELOCITY)
                    {
                        if (_velocityFile != null && _velocityFile.GridData != null && _velocityFile.GridData.VelocityGridData.Any())
                        {
                            if (!WriteBand(tiff, _velocityFile.GridData.EastVelocityData))
                                return false;

                            if (!WriteBand(tiff, _velocityFile.GridData.NorthVelocityData))
                                return false;

                            if (!WriteBand(tiff, _velocityFile.GridData.UpVelocityData))
                                return false;
                        }
                        else
                        {
                            if (Dimensions == 3 || Dimensions == 2)
                            {
                                if (!WriteBand(tiff, Ct2.GriEast.Data))
                                    return false;
                                
                                if (!WriteBand(tiff, Ct2.GriNorth.Data))
                                    return false;
                            }
                            if (Dimensions == 3 || Dimensions == 1)
                            {
                                if (!WriteBand(tiff, Gtx.GriHeight.Data))
                                    return false;
                            }
                        }
                    }
                    else if (TiffOutput == TiffOutputType.GEOCENTRIC_TRANSLATION)
                    {
                        if (!WriteBand(tiff, CommonPoints.GriX.Data))
                            return false;

                        if (!WriteBand(tiff, CommonPoints.GriY.Data))
                            return false;

                        if (!WriteBand(tiff, CommonPoints.GriZ.Data))
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

        // TODO: Type as template (etc. float, double...)
        internal bool WriteBand(Tiff tiff, List<float> bandList)
        {
            // TODO: Tiled vs. tiled
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
            var floatArray = new float[0];
                   
            int indexData = 0;

            for (var row = 0; row < NRows; row++)
            {
                int indexRow = row / TileSize;
                int indexTileRow = row % TileSize;

                for (var col = 0; col < NColumns; col++)
                {
                    int indexCol = col / TileSize;
                    int indexTileCol = col % TileSize;

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

        public override bool ClipGrid(double west_long, double south_lat, double east_long, double north_lat)
        {
            if (west_long < LowerLeftLongitude)
                return false;

            if (south_lat < LowerLeftLatitude)
                return false;

            if (east_long > UpperRightLongitude)
                return false;

            if (north_lat > UpperRightLatitude)
                return false;

            if (Dimensions == 3 || Dimensions == 2)
                return Ct2.ClipGrid(west_long, south_lat, east_long, north_lat) && Gtx.ClipGrid(west_long, south_lat, east_long, north_lat);
            
            return Gtx.ClipGrid(west_long, south_lat, east_long, north_lat);
        }

        public void TestGdalMetadata()
        {
            var gdaltest = new gridfiles.GDALMetadata();
            gdaltest.SerializeObject("GDAL_Metadata.xml");

            var item = SerializedObject<GDALMetadata>.SerializeToString(gdaltest);
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

        internal void ReadGeoTiffTags(FieldValue[] geoKeyDirectoryTag)
        {
            foreach (var fieldValue in geoKeyDirectoryTag)
            {
                var sourceArray = fieldValue.GetBytes().ToArray();

                if (sourceArray.Length <= 4)
                    continue;

                var byteArray = new Byte[8];
                var byteValue = new Byte[2];

                for (int i = 0; i < sourceArray.Length; i += 8)
                {
                    Buffer.BlockCopy(sourceArray, i, byteArray, 0, byteArray.Length);
                                        
                    Buffer.BlockCopy(byteArray , 0, byteValue, 0, byteValue.Length);
                    var tag = BitConverter.ToUInt16(byteValue);

                    Buffer.BlockCopy(byteArray, 2, byteValue, 0, byteValue.Length);
                    var type = BitConverter.ToUInt16(byteValue);

                    Buffer.BlockCopy(byteArray, 4, byteValue, 0, byteValue.Length);
                    var card = BitConverter.ToUInt16(byteValue);

                    Buffer.BlockCopy(byteArray, 6, byteValue, 0, byteValue.Length);
                    var roci = BitConverter.ToUInt16(byteValue);

                    switch (tag)
                    {
                        case 1:
                            break;
                        case 1024:
                            {
                                ModelType = (ModelTypeEnum) roci;
                                break;
                            }
                        case 1025:
                            {
                                RasterType = (RasterTypeEnum) roci;
                                break;
                            }
                        case 2048:
                            {
                                Epsg2d.SetCodeString("EPGS:" + roci);
                                break;
                            }
                        case 4096:
                            {
                                Epsg3d.SetCodeString("EPGS:" + roci);
                                break;
                            }
                        default:
                            break;
                    }
                }
            }
        }
    }
}
