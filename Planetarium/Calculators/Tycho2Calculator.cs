﻿using ADK;
using Planetarium.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Planetarium.Calculators
{
    public interface ITycho2Catalog
    {
        /// <summary>
        /// Gets stars in specified circular area
        /// </summary>
        /// <param name="eq">Equatorial coordinates of area center, at epoch J2000.0</param>
        /// <param name="angle">Area radius, in degrees</param>
        /// <param name="magFilter">Magnitude filter function, returns true if star is visible and should be included in results</param>
        /// <returns>Collection of <see cref="Tycho2Star"/> objects</returns>
        ICollection<Tycho2Star> GetStars(SkyContext context, CrdsEquatorial eq, double angle, Func<float, bool> magFilter);
    }

    public class Tycho2Calculator : BaseCalc, ICelestialObjectCalc<Tycho2Star>, ITycho2Catalog
    {
        /// <summary>
        /// Represents a single record from Tycho2 index file.
        /// </summary>
        private class Tycho2Region
        {
            /// <summary>
            /// First record id for this region (numeric 1-based index) in data file
            /// </summary>
            public long FirstStarId { get; set; }

            /// <summary>
            /// Last record id for this region (numeric 1-based index) in data file
            /// </summary>
            public long LastStarId { get; set; }

            /// <summary>
            /// Minimal Right Ascention of stars in this region
            /// </summary>
            public float RAmin { get; set; }

            /// <summary>
            /// Maximal Right Ascention of stars in this region
            /// </summary>
            public float RAmax { get; set; }

            /// <summary>
            /// Minimal Declination of stars in this region
            /// </summary>
            public float DECmin { get; set; }

            /// <summary>
            /// Maximal Declination of stars in this region
            /// </summary>
            public float DECmax { get; set; }
        }

        /// <summary>
        /// Parsed index file: collection of star regions
        /// </summary>
        private ICollection<Tycho2Region> IndexRegions = new List<Tycho2Region>();

        /// <summary>
        /// Binary Reader for accessing catalog data
        /// </summary>
        private BinaryReader CatalogReader;

        /// <summary>
        /// Length of catalog record, in bytes
        /// </summary>
        private const int CATALOG_RECORD_LEN = 33;

        /// <summary>
        /// Width of segment of celestial sphere (in degrees) that's defined by <see cref="Tycho2Region"/>.   
        /// </summary>
        private const double SEGMENT_WIDTH = 3.75;

        public override void Initialize()
        {
            // TODO take from settings
            string folder = "D:\\Tycho2";

            try
            {
                string indexFile = Path.Combine(folder, "tycho2.idx");
                string catalogFile = Path.Combine(folder, "tycho2.dat");

                // Read Tycho2 index file and load it into memory.

                StreamReader sr = new StreamReader(indexFile);

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    string[] chunks = line.Split(';');
                    Tycho2Region region = new Tycho2Region();
                    region.FirstStarId = Convert.ToInt64(chunks[0].Trim());
                    region.LastStarId = Convert.ToInt64(chunks[1].Trim());
                    region.RAmin = Convert.ToSingle(chunks[2].Trim(), CultureInfo.InvariantCulture);
                    region.RAmax = Convert.ToSingle(chunks[3].Trim(), CultureInfo.InvariantCulture);
                    region.DECmin = Convert.ToSingle(chunks[4].Trim(), CultureInfo.InvariantCulture);
                    region.DECmax = Convert.ToSingle(chunks[5].Trim(), CultureInfo.InvariantCulture);

                    IndexRegions.Add(region);
                }

                sr.Close();

                // Open Tycho2 catalog file
                CatalogReader = new BinaryReader(File.Open(catalogFile, FileMode.Open));
            }
            catch (Exception ex)
            {

            }
        }

        public ICollection<Tycho2Star> GetStars(SkyContext context, CrdsEquatorial eq, double angle, Func<float, bool> magFilter)
        {
            double ang = angle + SEGMENT_WIDTH / 2.0;

            var regions = IndexRegions.Where(r => Angle.Separation(eq, new CrdsEquatorial((r.RAmax + r.RAmin) / 2.0, (r.DECmax + r.DECmin) / 2.0)) <= ang);

            var stars = new List<Tycho2Star>();

            foreach (Tycho2Region r in regions)
            {
                stars.AddRange(GetStarsInRegion(context, r, eq, ang, magFilter));
            }

            return stars;
        }

        private PrecessionalElements GetPrecessionalElements(SkyContext context)
        {
            return Precession.ElementsFK5(Date.EPOCH_J2000, context.JulianDay);
        }

        private CrdsEquatorial GetEquatorialCoordinates(SkyContext context, Tycho2Star star)
        {
            PrecessionalElements p = context.Get(GetPrecessionalElements);
            double years = context.Get(YearsSinceEpoch);
                       
            // Take into account effect of proper motion:
            // now coordinates are for the mean equinox of J2000.0,
            // but for epoch of the target date
            var eq = star.Equatorial0 + new CrdsEquatorial(star.PmRA * years / 3600000, star.PmDec * years / 3600000);

            // Equatorial coordinates for the mean equinox and epoch of the target date
            CrdsEquatorial eq1 = Precession.GetEquatorialCoordinates(eq, p);

            // Nutation effect
            var eqN = Nutation.NutationEffect(eq1, context.NutationElements, context.Epsilon);

            // Aberration effect
            var eqA = Aberration.AberrationEffect(eq1, context.AberrationElements, context.Epsilon);

            // Apparent coordinates of the star
            eq1 += eqN + eqA;

            return eq1;
        }

        /// <summary>
        /// Calculates horizontal geocentric coordinates of star for current epoch
        /// </summary>
        /// <param name="context"><see cref="SkyContext"/> instance</param>
        /// <param name="star"><see cref="Tycho2Star"/> object</param>
        /// <returns>Horizontal geocentric coordinates of star for current epoch</returns>
        private CrdsHorizontal GetHorizontalCoordinates(SkyContext context, Tycho2Star star)
        {
            return context.Get(GetEquatorialCoordinates, star).ToHorizontal(context.GeoLocation, context.SiderealTime);
        }

        /// <summary>
        /// Gets rise, transit and set info for the star
        /// </summary>
        private RTS RiseTransitSet(SkyContext c, Tycho2Star star)
        {
            double theta0 = Date.ApparentSiderealTime(c.JulianDayMidnight, c.NutationElements.deltaPsi, c.Epsilon);
            var eq = c.Get(GetEquatorialCoordinates, star);
            return Visibility.RiseTransitSet(eq, c.GeoLocation, theta0);
        }

        private double YearsSinceEpoch(SkyContext context)
        {
            return (context.JulianDay - Date.EPOCH_J2000) / 365.25;
        }

        private ICollection<Tycho2Star> GetStarsInRegion(SkyContext context, Tycho2Region region, CrdsEquatorial eq, double angle, Func<float, bool> magFilter)
        {
            // seek reading position 
            CatalogReader.BaseStream.Seek(CATALOG_RECORD_LEN * (region.FirstStarId - 1), SeekOrigin.Begin);

            // count of records in current region
            int count = (int)(region.LastStarId - region.FirstStarId);

            // read region in memory for fast access
            byte[] buffer = CatalogReader.ReadBytes(CATALOG_RECORD_LEN * count);

            var stars = new List<Tycho2Star>();

            for (int i = 0; i < count; i++)
            {
                Tycho2Star star = ParseStarData(context, buffer, i * CATALOG_RECORD_LEN, eq, angle, magFilter);
                if (star != null)
                {
                    stars.Add(star);
                }
            }

            return stars;
        }

        private ICollection<Tycho2Star> GetStarsInRegion(Tycho2Region region, SkyContext context, short? tyc2, string tyc3)
        {
            // seek reading position 
            CatalogReader.BaseStream.Seek(CATALOG_RECORD_LEN * (region.FirstStarId - 1), SeekOrigin.Begin);

            // count of records in current region
            int count = (int)(region.LastStarId - region.FirstStarId);

            // read region in memory for fast access
            byte[] buffer = CatalogReader.ReadBytes(CATALOG_RECORD_LEN * count);

            var stars = new List<Tycho2Star>();

            for (int i = 0; i < count && stars.Count < 50; i++)
            {
                Tycho2Star star = ParseStarData(context, buffer, i * CATALOG_RECORD_LEN, tyc2, tyc3);
                if (star != null)
                {
                    stars.Add(star);
                }
            }

            return stars;
        }

        /// <summary>
        /// Reads data from catalog file as <see cref="Tycho2Star" /> instance. 
        /// </summary>
        /// <param name="buffer">Binary buffer with stars data</param>
        /// <param name="offset">Offset value to read the star record</param>
        /// <param name="eqCenter">Equatorial coordinates of map center, for J2000.0 epoch</param>
        /// <param name="angle">Maximal angular separation between map center and star coorinates (both coordinates for J2000.0 epoch)</param>        
        /// <remarks>
        /// Record format:
        /// [Tyc1][Tyc2][Tyc3][RA][Dec][PmRA][PmDec][Mag]
        /// [   2][   2][   1][ 8][  8][   4][    4][  4]
        /// </remarks>
        private Tycho2Star ParseStarData(SkyContext context, byte[] buffer, int offset, CrdsEquatorial eqCenter, double angle, Func<float, bool> magFilter)
        {
            float mag = BitConverter.ToSingle(buffer, offset + 29);
            if (magFilter(mag))
            {
                // Star coordinates at epoch J2000.0 
                var eq0 = new CrdsEquatorial(
                    BitConverter.ToDouble(buffer, offset + 5),
                    BitConverter.ToDouble(buffer, offset + 13));

                if (Angle.Separation(eq0, eqCenter) <= angle)
                {
                    return ReadStar(context, buffer, offset);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private Tycho2Star ParseStarData(SkyContext context, byte[] buffer, int offset, short? tyc2, string tyc3)
        {
            short t2 = BitConverter.ToInt16(buffer, offset + 2);
            char t3 = (char)buffer[offset + 4];

            if ((tyc2 == null || tyc2.Value == t2) && (string.IsNullOrEmpty(tyc3) || tyc3[0] == t3))
            {
                return ReadStar(context, buffer, offset);
            }
            else
            {
                return null;
            }
        }

        private Tycho2Star ReadStar(SkyContext context, byte[] buffer, int offset)
        {
            Tycho2Star star = new Tycho2Star();

            star.Equatorial0 = new CrdsEquatorial(
            BitConverter.ToDouble(buffer, offset + 5),
            BitConverter.ToDouble(buffer, offset + 13));
            star.Tyc1 = BitConverter.ToInt16(buffer, offset);
            star.Tyc2 = BitConverter.ToInt16(buffer, offset + 2);
            star.Tyc3 = (char)buffer[offset + 4];
            star.Magnitude = BitConverter.ToSingle(buffer, offset + 29);
            star.PmRA = BitConverter.ToSingle(buffer, offset + 21);
            star.PmDec = BitConverter.ToSingle(buffer, offset + 25);

            // current equatorial coordinates
            star.Equatorial = context.Get(GetEquatorialCoordinates, star);

            // current horizontal coordinates
            star.Horizontal = context.Get(GetHorizontalCoordinates, star);
            return star;
        }

        public override void Calculate(SkyContext context)
        {
            
        }

        public void ConfigureEphemeris(EphemerisConfig<Tycho2Star> e)
        {
            e.Add("Equatorial.Alpha", (c, s) => c.Get(GetEquatorialCoordinates, s).Alpha);
            e.Add("Equatorial.Delta", (c, s) => c.Get(GetEquatorialCoordinates, s).Delta);
            e.Add("Equatorial0.Alpha", (c, s) => s.Equatorial0.Alpha);
            e.Add("Equatorial0.Delta", (c, s) => s.Equatorial0.Delta);
            e.Add("Horizontal.Azimuth", (c, s) => c.Get(GetHorizontalCoordinates, s).Azimuth);
            e.Add("Horizontal.Altitude", (c, s) => c.Get(GetHorizontalCoordinates, s).Altitude);
            e.Add("RTS.Rise", (c, s) => c.Get(RiseTransitSet, s).Rise);
            e.Add("RTS.Transit", (c, s) => c.Get(RiseTransitSet, s).Transit);
            e.Add("RTS.Set", (c, s) => c.Get(RiseTransitSet, s).Set);
        }

        public CelestialObjectInfo GetInfo(SkyContext c, Tycho2Star s)
        {
            var rts = c.Get(RiseTransitSet, s);

            var info = new CelestialObjectInfo();
            info.SetSubtitle("Star").SetTitle(s.ToString())

            .AddRow("Constellation", Constellations.FindConstellation(s.Equatorial, c.JulianDay))

            .AddHeader("Equatorial coordinates (current epoch)")
            .AddRow("Equatorial.Alpha", s.Equatorial.Alpha)
            .AddRow("Equatorial.Delta", s.Equatorial.Delta)

            .AddHeader("Equatorial coordinates (J2000.0 epoch)")
            .AddRow("Equatorial.Alpha", s.Equatorial0.Alpha)
            .AddRow("Equatorial.Delta", s.Equatorial0.Delta)

            .AddHeader("Horizontal coordinates")
            .AddRow("Horizontal.Azimuth", s.Horizontal.Azimuth)
            .AddRow("Horizontal.Altitude", s.Horizontal.Altitude)

            .AddHeader("Visibility")
            .AddRow("RTS.Rise", rts.Rise, c.JulianDayMidnight + rts.Rise)
            .AddRow("RTS.Transit", rts.Transit, c.JulianDayMidnight + rts.Transit)
            .AddRow("RTS.Set", rts.Set, c.JulianDayMidnight + rts.Set)

            .AddHeader("Properties")
            .AddRow("Magnitude", s.Magnitude);

            return info;
        }

        private readonly Regex searchRegex = new Regex("tyc\\s*(?<tyc1>\\d+)((\\s*-\\s*|\\s+)(?<tyc2>\\d+)((\\s*-\\s*|\\s+)(?<tyc3>\\d+))?)?");

        public ICollection<SearchResultItem> Search(SkyContext context, string searchString, int maxCount = 50)
        {
            if (searchRegex.IsMatch(searchString.ToLowerInvariant()))
            {
                var match = searchRegex.Match(searchString.ToLowerInvariant());

                int tyc1 = int.Parse(match.Groups["tyc1"].Value);
                short? tyc2 = match.Groups["tyc2"].Success ? short.Parse(match.Groups["tyc2"].Value) : (short?)null;
                string tyc3 = match.Groups["tyc3"].Value;

                if (tyc1 > 0 && tyc1 <= 9537)
                {
                    Tycho2Region region = IndexRegions.ElementAt(tyc1 - 1);
                    var stars = GetStarsInRegion(region, context, tyc2, tyc3);

                    return stars.Take(maxCount).Select(s => new SearchResultItem(s, s.ToString())).ToList();
                }
            }

            return new List<SearchResultItem>();
        }

        public string GetName(Tycho2Star star)
        {
            return star.ToString();
        }
    }
}
