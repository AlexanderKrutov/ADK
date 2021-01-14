﻿using Astrarium.Algorithms;
using Astrarium.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace Astrarium.Plugins.Eclipses
{
    public class SolarEclipseVM : ViewModelBase
    {
        /// <summary>
        /// Directory to store maps cache
        /// </summary>
        public string CacheFolder { get; private set; }

        /// <summary>
        /// Selected Julian date
        /// </summary>
        public double JulianDay { get; set; }

        /// <summary>
        /// Date of the eclipse selected, converted to string
        /// </summary>
        public string EclipseDate { get; private set; }

        /// <summary>
        /// Eclipse description
        /// </summary>
        public string EclipseDescription { get; private set; }

        /// <summary>
        /// Saros series table
        /// </summary>
        public ObservableCollection<SarosSeriesItem> SarosSeries { get; private set; } = new ObservableCollection<SarosSeriesItem>();

        /// <summary>
        /// General eclipse info table
        /// </summary>
        public ObservableCollection<EclipseGeneralDetailsItem> EclipseGeneralDetails { get; private set; } = new ObservableCollection<EclipseGeneralDetailsItem>();

        /// <summary>
        /// Eclipse contacts info table
        /// </summary>
        public ObservableCollection<EclipseContactsItem> EclipseContacts { get; private set; } = new ObservableCollection<EclipseContactsItem>();

        /// <summary>
        /// Besselian elements table
        /// </summary>
        public ObservableCollection<BesselianElementsTableItem> BesselianElementsTable { get; private set; } = new ObservableCollection<BesselianElementsTableItem>();

        public string BesselianElementsTableFooter { get; private set; }
        public string BesselianElementsTableHeader { get; private set; }

        /// <summary>
        /// Flag indicating calculation is in progress
        /// </summary>
        public bool IsCalculating { get; private set; }

        /// <summary>
        /// Flag indicating previous saros button is enabled
        /// </summary>
        public bool PrevSarosEnabled { get; private set; }

        /// <summary>
        /// Flag indicating next saros button is enabled
        /// </summary>
        public bool NextSarosEnabled { get; private set; }

        public ICommand PrevEclipseCommand => new Command(PrevEclipse);
        public ICommand NextEclipseCommand => new Command(NextEclipse);
        public ICommand PrevSarosCommand => new Command(PrevSaros);
        public ICommand NextSarosCommand => new Command(NextSaros);
        public ICommand ClickOnMapCommand => new Command(ClickOnMap);
        public ICommand ClickOnLinkCommand => new Command<double>(ClickOnLink);

        private int selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => selectedTabIndex; 
            set
            {
                selectedTabIndex = value;
                CalculateSarosSeries();
            }
        } 

        /// <summary>
        /// Collection of map tile servers to switch between them
        /// </summary>
        public ICollection<ITileServer> TileServers { get; private set; }

        /// <summary>
        /// Collection of markers (points) on the map
        /// </summary>
        public ICollection<Marker> Markers { get; private set; }

        /// <summary>
        /// Collection of tracks (lines) on the map
        /// </summary>
        public ICollection<Track> Tracks { get; private set; }

        /// <summary>
        /// Collection of polygons (areas) on the map
        /// </summary>
        public ICollection<Polygon> Polygons { get; private set; }

        private readonly ISky sky;
        private readonly IEclipsesCalculator eclipsesCalculator;
        private readonly ISettings settings;
        private CrdsGeographical observerLocation;
        private PolynomialBesselianElements be;
        private static NumberFormatInfo nf;

        private static readonly IEphemFormatter fmtGeo = new Formatters.GeoCoordinatesFormatter();
        private static readonly IEphemFormatter fmtTime = new Formatters.TimeFormatter(withSeconds: true);

        #region Map styles

        private readonly MarkerStyle riseSetMarkerStyle = new MarkerStyle(5, Brushes.Red, null, Brushes.Red, SystemFonts.DefaultFont, StringFormat.GenericDefault);
        private readonly MarkerStyle centralLineMarkerStyle = new MarkerStyle(5, Brushes.Black, null, Brushes.Black, SystemFonts.DefaultFont, StringFormat.GenericDefault);
        private readonly MarkerStyle maxPointMarkerStyle = new MarkerStyle(5, Brushes.Red, null, Brushes.Black, SystemFonts.DefaultFont, StringFormat.GenericDefault);
        private readonly TrackStyle riseSetTrackStyle = new TrackStyle(new Pen(Color.Red, 2));
        private readonly TrackStyle penumbraLimitTrackStyle = new TrackStyle(new Pen(Color.Orange, 2));
        private readonly TrackStyle umbraLimitTrackStyle = new TrackStyle(new Pen(Color.Gray, 2));
        private readonly TrackStyle centralLineTrackStyle = new TrackStyle(new Pen(Color.Black, 2));
        private readonly PolygonStyle umbraPolygonStyle = new PolygonStyle(new SolidBrush(Color.FromArgb(100, Color.Gray)));
        private readonly MarkerStyle observerLocationMarkerStyle = new MarkerStyle(5, Brushes.Black, null, Brushes.Black, SystemFonts.DefaultFont, StringFormat.GenericDefault);

        #endregion Map styles

        public ITileServer TileServer
        {
            get => GetValue<ITileServer>(nameof(TileServer));
            set  
            { 
                SetValue(nameof(TileServer), value);
                TileImageAttributes = GetImageAttributes();
                if (settings.Get<string>("EclipseMapTileServer") != value.Name)
                {
                    settings.Set("EclipseMapTileServer", value.Name);
                    settings.Save();
                }
            }
        }

        public ImageAttributes TileImageAttributes
        {
            get => GetValue<ImageAttributes>(nameof(TileImageAttributes));
            set => SetValue(nameof(TileImageAttributes), value);
        }

        static SolarEclipseVM()
        {
            nf = new NumberFormatInfo();
            nf.NumberDecimalSeparator = ".";
            nf.NumberGroupSeparator = "\u2009";
        }

        public SolarEclipseVM(IEclipsesCalculator eclipsesCalculator, ISky sky, ISettings settings)
        {
            this.sky = sky;
            this.eclipsesCalculator = eclipsesCalculator;
            this.settings = settings;
            this.settings.PropertyChanged += Settings_PropertyChanged;
            observerLocation = settings.Get<CrdsGeographical>("ObserverLocation");
            
            CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Astrarium", "MapsCache");

            TileServers = new List<ITileServer>() 
            {
                new OfflineTileServer(),
                new OpenStreetMapTileServer("Astrarium v1.0 contact astrarium@astrarium.space"),
                new StamenTerrainTileServer(),
                new OpenTopoMapServer()
            };

            string tileServerName = settings.Get<string>("EclipseMapTileServer");
            var tileServer = TileServers.FirstOrDefault(s => s.Name.Equals(tileServerName));            
            TileServer = tileServer ?? TileServers.First();

            JulianDay = sky.Context.JulianDay - LunarEphem.SINODIC_PERIOD;

            CalculateEclipse(next: true, saros: false);
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Schema")
            {
                TileImageAttributes = GetImageAttributes();
            }
        }

        private void PrevEclipse()
        {
            CalculateEclipse(next: false, saros: false);
        }

        private void NextEclipse()
        {
            CalculateEclipse(next: true, saros: false);
        }

        private void PrevSaros()
        {
            CalculateEclipse(next: false, saros: true);
        }

        private void NextSaros()
        {
            CalculateEclipse(next: true, saros: true);
        }

        public string Details { get; set; }

        public GeoPoint MapMouse
        {
            get => GetValue<GeoPoint>(nameof(MapMouse));
            set
            {
                SetValue(nameof(MapMouse), value);

                var pos = new CrdsGeographical(-value.Longitude, value.Latitude);
                var local = SolarEclipses.LocalCircumstances(be, pos);

                Details = local.ToString();

                NotifyPropertyChanged(nameof(Details));
            }
        }

        private void ClickOnMap()
        {
            var location = MapMouse;

            observerLocation = new CrdsGeographical(-location.Longitude, location.Latitude, 0, 0, "UTC+0", "Selected location");
            Markers.Remove(Markers.Last());
            AddLocationMarker();
        }

        private void ClickOnLink(double jd)
        {
            JulianDay = jd - LunarEphem.SINODIC_PERIOD / 2;
            CalculateEclipse(next: true, saros: false);
        }

        private ImageAttributes GetImageAttributes()
        {
            // make image "red"
            if (settings.Get<ColorSchema>("Schema") == ColorSchema.Red)
            {
                float[][] matrix = {
                    new float[] {0.3f, 0, 0, 0, 0},
                    new float[] {0.3f, 0, 0, 0, 0},
                    new float[] {0.3f, 0, 0, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 0}
                };
                var colorMatrix = new ColorMatrix(matrix);
                var attr = new ImageAttributes();
                attr.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                return attr;
            }

            // make image lighten
            if (TileServer is OfflineTileServer)
            {
                float gamma = 1;
                float brightness = 1.2f;
                float contrast = 1;
                float alpha = 1;

                float adjustedBrightness = brightness - 1.0f;

                float[][] matrix ={
                    new float[] {contrast, 0, 0, 0, 0}, // scale red
                    new float[] {0, contrast, 0, 0, 0}, // scale green
                    new float[] {0, 0, contrast, 0, 0}, // scale blue
                    new float[] {0, 0, 0, alpha, 0},
                    new float[] {adjustedBrightness, adjustedBrightness, adjustedBrightness, 0, 1}};

                var attr = new ImageAttributes();
                attr.ClearColorMatrix();
                attr.SetColorMatrix(new ColorMatrix(matrix), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                attr.SetGamma(gamma, ColorAdjustType.Bitmap);

                return attr;
            }
            else
            {
                return null;
            }
        }

        private async void CalculateEclipse(bool next, bool saros)
        {
            IsCalculating = true;
            NotifyPropertyChanged(nameof(IsCalculating));

            SolarEclipse eclipse = SolarEclipses.NearestEclipse(JulianDay + (next ? 1 : -1) * (saros ? LunarEphem.SAROS : LunarEphem.SINODIC_PERIOD), next);
            JulianDay = eclipse.JulianDayMaximum;
            EclipseDate = Formatters.Date.Format(new Date(JulianDay, observerLocation.UtcOffset));
            be = eclipsesCalculator.GetBesselianElements(JulianDay);
            string type = eclipse.EclipseType.ToString();
            string subtype = eclipse.IsNonCentral ? " non-central" : "";
            EclipseDescription = $"{type}{subtype} solar eclipse";
            PrevSarosEnabled = SolarEclipses.NearestEclipse(JulianDay - LunarEphem.SAROS, next: false).Saros == eclipse.Saros;
            NextSarosEnabled = SolarEclipses.NearestEclipse(JulianDay + LunarEphem.SAROS, next: true).Saros == eclipse.Saros;
            
            NotifyPropertyChanged(
                nameof(EclipseDate), 
                nameof(EclipseDescription), 
                nameof(PrevSarosEnabled),
                nameof(NextSarosEnabled));

            await Task.Run(() =>
            {
                var map = SolarEclipses.EclipseMap(be);

                var tracks = new List<Track>();
                var polygons = new List<Polygon>();
                var markers = new List<Marker>();

                if (map.P1 != null)
                {
                    markers.Add(new Marker(ToGeo(map.P1), riseSetMarkerStyle, "P1"));
                }
                if (map.P2 != null)
                {
                    markers.Add(new Marker(ToGeo(map.P2), riseSetMarkerStyle, "P2"));
                }
                if (map.P3 != null)
                {
                    markers.Add(new Marker(ToGeo(map.P3), riseSetMarkerStyle, "P3"));
                }
                if (map.P4 != null)
                {
                    markers.Add(new Marker(ToGeo(map.P4), riseSetMarkerStyle, "P4"));
                }
                if (map.C1 != null)
                {
                    markers.Add(new Marker(ToGeo(map.C1), centralLineMarkerStyle, "C1"));
                }
                if (map.C2 != null)
                {
                    markers.Add(new Marker(ToGeo(map.C2), centralLineMarkerStyle, "C2"));
                }

                for (int i = 0; i < 2; i++)
                {
                    if (map.UmbraNorthernLimit[i].Any())
                    {
                        var track = new Track(umbraLimitTrackStyle);
                        track.AddRange(map.UmbraNorthernLimit[i].Select(p => ToGeo(p)));
                        tracks.Add(track);
                    }

                    if (map.UmbraSouthernLimit[i].Any())
                    {
                        var track = new Track(umbraLimitTrackStyle);
                        track.AddRange(map.UmbraSouthernLimit[i].Select(p => ToGeo(p)));
                        tracks.Add(track);
                    }
                }

                if (map.TotalPath.Any())
                {
                    var track = new Track(centralLineTrackStyle);
                    track.AddRange(map.TotalPath.Select(p => ToGeo(p)));
                    tracks.Add(track);
                }

                // central line is divided into 2 ones => draw shadow path as 2 polygons

                if ((map.UmbraNorthernLimit[0].Any() && !map.UmbraNorthernLimit[1].Any()) ||
                    (map.UmbraSouthernLimit[0].Any() && !map.UmbraSouthernLimit[1].Any()))
                {
                    var polygon = new Polygon(umbraPolygonStyle);
                    polygon.AddRange(map.UmbraNorthernLimit[0].Select(p => ToGeo(p)));
                    polygon.AddRange(map.UmbraNorthernLimit[1].Select(p => ToGeo(p)));
                    if (map.C2 != null) polygon.Add(ToGeo(map.C2));
                    polygon.AddRange((map.UmbraSouthernLimit[1] as IEnumerable<CrdsGeographical>).Reverse().Select(p => ToGeo(p)));
                    polygon.AddRange((map.UmbraSouthernLimit[0] as IEnumerable<CrdsGeographical>).Reverse().Select(p => ToGeo(p)));
                    if (map.C1 != null) polygon.Add(ToGeo(map.C1));
                    polygons.Add(polygon);
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (map.UmbraNorthernLimit[i].Any() && map.UmbraSouthernLimit[i].Any())
                        {
                            var polygon = new Polygon(umbraPolygonStyle);
                            polygon.AddRange(map.UmbraNorthernLimit[i].Select(p => ToGeo(p)));
                            polygon.AddRange((map.UmbraSouthernLimit[i] as IEnumerable<CrdsGeographical>).Reverse().Select(p => ToGeo(p)));
                            polygons.Add(polygon);
                        }
                    }
                }

                foreach (var curve in map.RiseSetCurve)
                {
                    if (curve.Any())
                    {
                        var track = new Track(riseSetTrackStyle);
                        track.AddRange(curve.Select(p => ToGeo(p)));
                        track.Add(track.First());
                        tracks.Add(track);
                    }
                }

                if (map.PenumbraNorthernLimit.Any())
                {
                    var track = new Track(penumbraLimitTrackStyle);
                    track.AddRange(map.PenumbraNorthernLimit.Select(p => ToGeo(p)));
                    tracks.Add(track);
                }

                if (map.PenumbraSouthernLimit.Any())
                {
                    var track = new Track(penumbraLimitTrackStyle);
                    track.AddRange(map.PenumbraSouthernLimit.Select(p => ToGeo(p)));
                    tracks.Add(track);
                }

                if (map.Max != null)
                {
                    markers.Add(new Marker(ToGeo(map.Max), maxPointMarkerStyle, "Max"));
                }

                var maxCirc = SolarEclipses.LocalCircumstances(be, map.Max);

                // TODO:
                // add Sun/Moon info for greatest eclipse:
                // https://eclipse.gsfc.nasa.gov/SEplot/SEplot2001/SE2022Apr30P.GIF

                var eclipseGeneralDetails = new ObservableCollection<EclipseGeneralDetailsItem>()
                {
                    new EclipseGeneralDetailsItem("Type", $"{type}{subtype}"),
                    new EclipseGeneralDetailsItem("Saros", $"{eclipse.Saros}"),
                    new EclipseGeneralDetailsItem("Date", $"{EclipseDate}"),
                    new EclipseGeneralDetailsItem("Magnitude", $"{eclipse.Magnitude.ToString("N5", nf)}"),
                    new EclipseGeneralDetailsItem("Gamma", $"{eclipse.Gamma.ToString("N5", nf)}"),
                    new EclipseGeneralDetailsItem("Maximal Duration", $"{fmtTime.Format(maxCirc.TotalDuration) }"),
                    new EclipseGeneralDetailsItem("ΔT", $"{be.DeltaT.ToString("N1", nf) } s")
                };
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    EclipseGeneralDetails = eclipseGeneralDetails;
                });

                var eclipseContacts = new ObservableCollection<EclipseContactsItem>();
                eclipseContacts.Add(new EclipseContactsItem("P1 (First external contact)", map.P1));
                if (map.P2 != null)
                {
                    eclipseContacts.Add(new EclipseContactsItem("P2 (First internal contact)", map.P2));
                }
                if (map.C1 != null && !double.IsNaN(map.C1.JulianDay))
                {
                    eclipseContacts.Add(new EclipseContactsItem("C1 (First umbra contact)", map.C1));
                }
                eclipseContacts.Add(new EclipseContactsItem("Max (Greatest Eclipse)", map.Max));   
                if (map.C2 != null && !double.IsNaN(map.C2.JulianDay))
                {
                    eclipseContacts.Add(new EclipseContactsItem("C2 (Last umbra contact)", map.C2));
                }
                if (map.P3 != null)
                {
                    eclipseContacts.Add(new EclipseContactsItem("P3 (Last internal contact)", map.P3));
                }
                eclipseContacts.Add(new EclipseContactsItem("P4 (Last external contact)", map.P4));    
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    EclipseContacts = eclipseContacts;
                });


                var besselianElementsTable = new ObservableCollection<BesselianElementsTableItem>();
                for (int i=0; i<4; i++)
                {
                    besselianElementsTable.Add(new BesselianElementsTableItem(i, be));
                }
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    BesselianElementsTable = besselianElementsTable;
                });

                BesselianElementsTableHeader = $"Elements for t\u2080 = {Formatters.DateTime.Format(new Date(be.JulianDay0))} TDT (JDE = { be.JulianDay0.ToString("N6", nf)})";

                var tanFInfo = new StringBuilder();
                tanFInfo.AppendLine($"Tan ƒ1 = {be.TanF1.ToString("N7", nf)}");
                tanFInfo.AppendLine($"Tan ƒ2 = {be.TanF2.ToString("N7", nf)}");
                BesselianElementsTableFooter = tanFInfo.ToString();

                Tracks = tracks;
                Polygons = polygons;
                Markers = markers;
                IsCalculating = false;

                AddLocationMarker();

                CalculateSarosSeries();

                NotifyPropertyChanged(
                    nameof(IsCalculating),
                    nameof(Tracks),
                    nameof(Polygons),
                    nameof(Markers),
                    nameof(EclipseGeneralDetails),
                    nameof(EclipseContacts),
                    nameof(BesselianElementsTable),
                    nameof(BesselianElementsTableHeader),
                    nameof(BesselianElementsTableFooter)
                );
            });
        }

        private void AddLocationMarker()
        {
            Markers.Add(new Marker(ToGeo(observerLocation), observerLocationMarkerStyle, observerLocation.LocationName));
            Markers = new List<Marker>(Markers);            
            NotifyPropertyChanged(nameof(Markers));
        }

        private async void CalculateSarosSeries()
        {
            await Task.Run(() =>
            {
                if (SelectedTabIndex != 2) return;

                IsCalculating = true;
                NotifyPropertyChanged(nameof(IsCalculating));

                double jd = JulianDay;
                List<SolarEclipse> eclipses = new List<SolarEclipse>();

                // add current eclipse
                var eclipse = SolarEclipses.NearestEclipse(jd, true);
                eclipses.Add(eclipse);
                int saros = eclipse.Saros;

                // add previous eclipses
                do
                {
                    jd -= LunarEphem.SAROS;
                    eclipse = SolarEclipses.NearestEclipse(jd, false);
                    if (eclipse.Saros == saros)
                    {
                        eclipses.Insert(0, eclipse);
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);

                jd = JulianDay;
                // add next eclipses
                do
                {
                    jd += LunarEphem.SAROS;
                    eclipse = SolarEclipses.NearestEclipse(jd, true);
                    if (eclipse.Saros == saros)
                    {
                        eclipses.Add(eclipse);
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);

                ObservableCollection<SarosSeriesItem> sarosSeries = new ObservableCollection<SarosSeriesItem>();

                foreach (var e in eclipses)
                {
                    string type = e.EclipseType.ToString();
                    string subtype = e.IsNonCentral ? " non-central" : "";
                    var pbe = eclipsesCalculator.GetBesselianElements(e.JulianDayMaximum);
                    var local = SolarEclipses.LocalCircumstances(pbe, observerLocation);
                    sarosSeries.Add(new SarosSeriesItem()
                    {
                        JulianDay = e.JulianDayMaximum,
                        Date = Formatters.Date.Format(new Date(e.JulianDayMaximum, 0)),
                        Type = $"{type}{subtype}",
                        Gamma = e.Gamma.ToString("N5", nf),
                        Magnitude = e.Magnitude.ToString("N5", nf),
                        LocalVisibility = eclipsesCalculator.GetLocalVisibilityString(eclipse, local)
                    });
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SarosSeries = sarosSeries;
                });

                IsCalculating = false;
                NotifyPropertyChanged(nameof(SarosSeries), nameof(IsCalculating));
            });
        }

        private GeoPoint ToGeo(CrdsGeographical g)
        {
            return new GeoPoint((float)-g.Longitude, (float)g.Latitude);
        }

        public class SarosSeriesItem
        {
            public double JulianDay { get; set; }
            public string Date { get; set; }
            public string Type { get; set; }
            public string Gamma { get; set; }
            public string Magnitude { get; set; }
            public string LocalVisibility { get; set; }
        }

        public class EclipseGeneralDetailsItem
        {
            public string Name { get; set; }
            public string Value { get; set; }

            public EclipseGeneralDetailsItem(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }

        public class EclipseContactsItem
        {
            public string Point { get; set; }
            public string Coordinates { get; set; }
            public string Time { get; set; }

            public EclipseContactsItem(string text, SolarEclipseMapPoint p)
            {
                Point = text;
                Coordinates = fmtGeo.Format(p);
                Time = $"{fmtTime.Format(new Date(p.JulianDay, 0))} UT";
            }
        }

        public class BesselianElementsTableItem
        {
            public string Index { get; set; }
            public string X { get; set; }
            public string Y { get; set; }
            public string D { get; set; }
            public string L1 { get; set; }
            public string L2 { get; set; }
            public string Mu { get; set; }

            public BesselianElementsTableItem(int index, PolynomialBesselianElements pbe)
            {
                Index = index.ToString();
                X = pbe.X[index].ToString("N6", nf);
                Y = pbe.Y[index].ToString("N6", nf);

                if (index <= 2)
                {
                    D = pbe.D[index].ToString("N6", nf);
                    L1 = pbe.L1[index].ToString("N6", nf);
                    L2 = pbe.L2[index].ToString("N6", nf);
                }

                if (index <= 1)
                    Mu = Angle.To360(pbe.Mu[index]).ToString("N6", nf);
            }
        }

        public class BesselianElementsFTableItem
        {
            public string Text { get; set; }
            public string Value { get; set; }

            public BesselianElementsFTableItem(string text, string value)
            {
                Text = text;
                Value = value;
            }
        }
    }
}
