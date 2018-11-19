﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADK.Demo.Calculators
{
    public class CelestialGridCalc : BaseSkyCalc
    {
        public CelestialGridCalc(Sky sky) : base(sky)
        {
            // Ecliptic
            CelestialGrid LineEcliptic = new CelestialGrid("Ecliptic", 1, 24);
            LineEcliptic.FromHorizontal = (h) =>
            {
                var eq = h.ToEquatorial(Sky.GeoLocation, Sky.SiderealTime);
                var ec = eq.ToEcliptical(Sky.Epsilon);
                return new GridPoint(ec.Lambda, ec.Beta);
            };
            LineEcliptic.ToHorizontal = (c) =>
            {
                var ec = new CrdsEcliptical(c.Longitude, c.Latitude);
                var eq = ec.ToEquatorial(Sky.Epsilon);
                return eq.ToHorizontal(Sky.GeoLocation, Sky.SiderealTime);
            };
            Sky.AddDataProvider("LineEcliptic", () => LineEcliptic);

            // Horizontal grid
            CelestialGrid GridHorizontal = new CelestialGrid("Horizontal", 17, 24);
            GridHorizontal.FromHorizontal = (h) => new GridPoint(h.Azimuth, h.Altitude);
            GridHorizontal.ToHorizontal = (c) => new CrdsHorizontal(c.Longitude, c.Latitude);
            Sky.AddDataProvider("GridHorizontal", () => GridHorizontal);

            // Equatorial grid
            CelestialGrid GridEquatorial = new CelestialGrid("Equatorial", 17, 24);
            GridEquatorial.FromHorizontal = (h) =>
            {
                var eq = h.ToEquatorial(Sky.GeoLocation, Sky.SiderealTime);
                return new GridPoint(eq.Alpha, eq.Delta);
            };
            GridEquatorial.ToHorizontal = (c) =>
            {
                var eq = new CrdsEquatorial(c.Longitude, c.Latitude);
                return eq.ToHorizontal(Sky.GeoLocation, Sky.SiderealTime);
            };
            Sky.AddDataProvider("GridEquatorial", () => GridEquatorial);
        }
    }
}