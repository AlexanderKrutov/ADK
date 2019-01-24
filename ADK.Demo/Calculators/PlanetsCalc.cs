﻿using ADK.Demo.Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADK.Demo.Calculators
{
    public class PlanetsCalc : BaseSkyCalc, IEphemProvider<Planet>, IInfoProvider<Planet>
    {
        private Planet[] Planets = new Planet[8];

        private RingsAppearance SaturnRings = new RingsAppearance();

        private string[] PlanetNames = new string[]
        {
            "Mercury",
            "Venus",
            "Earth",
            "Mars",
            "Jupiter",
            "Saturn",
            "Uranus",
            "Neptune"
        };

        public PlanetsCalc(Sky sky) : base(sky)
        {
            for (int i = 0; i < Planets.Length; i++)
            {
                Planets[i] = new Planet() { Number = i + 1, Names = new string[] { PlanetNames[i] } };
            }

            Planets[Planet.JUPITER - 1].Flattening = 0.064874f;
            Planets[Planet.SATURN - 1].Flattening = 0.097962f;

            Sky.AddDataProvider("Planets", () => Planets);
            Sky.AddDataProvider("SaturnRings", () => SaturnRings);
        }

        /// <summary>
        /// Get heliocentrical coordinates of Earth
        /// </summary>
        private CrdsHeliocentrical EarthHeliocentrial(SkyContext c)
        {
            return PlanetPositions.GetPlanetCoordinates(Planet.EARTH, c.JulianDay);
        }

        /// <summary>
        /// Gets ecliptical coordinates of Sun
        /// </summary>
        private CrdsEcliptical SunEcliptical(SkyContext c)
        {
            CrdsHeliocentrical hEarth = c.Get(EarthHeliocentrial);
            var sunEcliptical = new CrdsEcliptical(Angle.To360(hEarth.L + 180), -hEarth.B, hEarth.R);

            // Corrected solar coordinates to FK5 system
            sunEcliptical += PlanetPositions.CorrectionForFK5(c.JulianDay, sunEcliptical);

            // Add nutation effect to ecliptical coordinates of the Sun
            sunEcliptical += Nutation.NutationEffect(c.NutationElements.deltaPsi);

            // Add aberration effect, so we have an final ecliptical coordinates of the Sun 
            sunEcliptical += Aberration.AberrationEffect(sunEcliptical.Distance);

            return sunEcliptical;
        }

        /// <summary>
        /// Gets heliocentrical coordinates of planet
        /// </summary>
        private CrdsHeliocentrical Heliocentrical(SkyContext c, int p)
        {
            // final difference to stop iteration process, 1 second of time
            double deltaTau = TimeSpan.FromSeconds(1).TotalDays;

            // time taken by the light to reach the Earth
            double tau = 0;

            // previous value of tau to calculate the difference
            double tau0 = 1;

            // Heliocentrical coordinates of planet
            CrdsHeliocentrical planetHeliocentrial = null;

            // Heliocentrical coordinates of Earth
            CrdsHeliocentrical hEarth = c.Get(EarthHeliocentrial);

            // Iterative process to find heliocentrical coordinates of planet
            while (Math.Abs(tau - tau0) > deltaTau)
            {
                // Heliocentrical coordinates of planet
                planetHeliocentrial = PlanetPositions.GetPlanetCoordinates(p, c.JulianDay - tau, highPrecision: true);

                // Ecliptical coordinates of planet
                var planetEcliptical = planetHeliocentrial.ToRectangular(hEarth).ToEcliptical();

                tau0 = tau;
                tau = PlanetPositions.LightTimeEffect(planetEcliptical.Distance);
            }

            return planetHeliocentrial;
        }

        /// <summary>
        /// Gets ecliptical coordinates of Earth
        /// </summary>
        private CrdsEcliptical Ecliptical(SkyContext c, int p)
        {
            // Heliocentrical coordinates of planet
            CrdsHeliocentrical heliocentrical = c.Get(Heliocentrical, p);

            // Heliocentrical coordinates of Earth
            CrdsHeliocentrical hEarth = c.Get(EarthHeliocentrial);

            // Ecliptical coordinates of planet
            var ecliptical = heliocentrical.ToRectangular(hEarth).ToEcliptical();

            // Correction for FK5 system
            ecliptical += PlanetPositions.CorrectionForFK5(c.JulianDay, ecliptical);

            // Take nutation into account
            ecliptical += Nutation.NutationEffect(c.NutationElements.deltaPsi);

            return ecliptical;
        }

        /// <summary>
        /// Gets topocentrical equatorial coordinates of Earth
        /// </summary>
        private CrdsEquatorial Equatorial0(SkyContext c, int p)
        {
            return c.Get(Ecliptical, p).ToEquatorial(c.Epsilon);
        }

        /// <summary>
        /// Gets distance from Earth to planet
        /// </summary>
        private double DistanceFromEarth(SkyContext c, int p)
        {
            return c.Get(Ecliptical, p).Distance;
        }

        /// <summary>
        /// Gets distance from planet to Sun
        /// </summary>
        private double DistanceFromSun(SkyContext c, int p)
        {
            return c.Get(Heliocentrical, p).R;
        }

        /// <summary>
        /// Gets visible semidianeter of planet
        /// </summary>
        private double Semidiameter(SkyContext c, int p)
        {
            return PlanetEphem.Semidiameter(p, c.Get(DistanceFromEarth, p));
        }

        /// <summary>
        /// Gets horizontal parallax of planet
        /// </summary>
        private double Parallax(SkyContext c, int p)
        {
            return PlanetEphem.Parallax(c.Get(DistanceFromEarth, p));
        }

        /// <summary>
        /// Gets apparent topocentric coordinates of planet
        /// </summary>
        private CrdsEquatorial Equatorial(SkyContext c, int p)
        {
            return c.Get(Equatorial0, p).ToTopocentric(c.GeoLocation, c.SiderealTime, c.Get(Parallax, p));
        }

        /// <summary>
        /// Gets apparent horizontal coordinates of planet
        /// </summary>
        private CrdsHorizontal Horizontal(SkyContext c, int p)
        {
            return c.Get(Equatorial, p).ToHorizontal(c.GeoLocation, c.SiderealTime);
        }

        /// <summary>
        /// Gets elongation angle for the planet
        /// </summary>
        private double Elongation(SkyContext c, int p)
        {
            return ADK.Appearance.Elongation(c.Get(this.SunEcliptical), c.Get(Ecliptical, p));
        }

        /// <summary>
        /// Gets phase angle for the planet
        /// </summary>
        private double PhaseAngle(SkyContext c, int p)
        {
            return ADK.Appearance.PhaseAngle(c.Get(this.Elongation, p), c.Get(SunEcliptical).Distance, c.Get(DistanceFromEarth, p));
        }

        /// <summary>
        /// Gets phase for the planet
        /// </summary>
        private double Phase(SkyContext c, int p)
        {
            return ADK.Appearance.Phase(c.Get(this.PhaseAngle, p));
        } 

        /// <summary>
        /// Gets visible magnitude of the planet
        /// </summary>
        private float Magnitude(SkyContext c, int p)
        {
            float mag = PlanetEphem.Magnitude(p, c.Get(DistanceFromEarth, p), c.Get(DistanceFromSun, p), c.Get(PhaseAngle, p));
            if (p == Planet.SATURN)
            {
                var saturnRings = PlanetEphem.SaturnRings(c.JulianDay, c.Get(Heliocentrical, p), c.Get(EarthHeliocentrial), c.Epsilon);
                mag += saturnRings.GetRingsMagnitude();
            }

            return mag;
        }

        /// <summary>
        /// Gets visual appearance for the planet
        /// </summary>
        private PlanetAppearance Appearance(SkyContext c, int p)
        {
            return PlanetEphem.PlanetAppearance(c.JulianDay, p, c.Get(Equatorial0, p), c.Get(DistanceFromEarth, p));
        }

        /// <summary>
        /// Gets rise, transit and set info for the planet
        /// </summary>
        private RTS RiseTransitSet(SkyContext c, int p)
        {
            double jd = c.JulianDayMidnight;
            double theta0 = Date.ApparentSiderealTime(jd, c.NutationElements.deltaPsi, c.Epsilon);
            double parallax = c.Get(Parallax, p);

            CrdsEquatorial[] eq = new CrdsEquatorial[3];
            double[] diff = new double[] { 0, 0.5, 1 };

            for (int i = 0; i < 3; i++)
            {
                eq[i] = new SkyContext(jd + diff[i], c.GeoLocation).Get(Equatorial0, p);
            }

            return ADK.Appearance.RiseTransitSet(eq, c.GeoLocation, theta0, parallax);
        }

        public override void Calculate(SkyContext context)
        {
            foreach (var p in Planets)
            {
                if (p.Number == Planet.EARTH) continue;

                int n = p.Number;

                p.Equatorial = context.Get(Equatorial, n);
                p.Horizontal = context.Get(Horizontal, n);
                p.Appearance = context.Get(Appearance, n);
                p.Magnitude = context.Get(Magnitude, n);
                p.Semidiameter = context.Get(Semidiameter, n);
                p.Phase = context.Get(Phase, n);
                p.Ecliptical = context.Get(Ecliptical, n);

                if (p.Number == Planet.SATURN)
                {
                    SaturnRings = context.Get(GetSaturnRings, n);
                }
            }
        }

        private RingsAppearance GetSaturnRings(SkyContext c, int p)
        {
            return PlanetEphem.SaturnRings(c.JulianDay, c.Get(Heliocentrical, p), c.Get(EarthHeliocentrial), c.Epsilon);
        }

        public void ConfigureEphemeris(EphemerisConfig<Planet> e)
        {
            e.Add("Magnitude", (c, p) => c.Get(Magnitude, p.Number));

            e.Add("Horizontal.Altitude", (c, p) => c.Get(Horizontal, p.Number).Altitude);
            e.Add("Horizontal.Azimuth", (c, p) => c.Get(Horizontal, p.Number).Azimuth);

            e.Add("Equatorial.Alpha", (c, p) => c.Get(Equatorial, p.Number).Alpha)
                .WithFormatter(Formatters.RA);

            e.Add("Equatorial.Delta", (c, p) => c.Get(Equatorial, p.Number).Delta)
                .WithFormatter(Formatters.Dec);

            e.Add("SaturnRings.a", (c, p) => c.Get(GetSaturnRings, p.Number).a)
                .AvailableIf((c, p) => p.Number == Planet.SATURN);

            e.Add("SaturnRings.b", (c, p) => c.Get(GetSaturnRings, p.Number).b)
                .AvailableIf((c, p) => p.Number == Planet.SATURN);

            e.Add("RTS.Rise", (c, p) => c.Get(RiseTransitSet, p.Number).Rise)
                .WithFormatter(Formatters.Time);

            e.Add("RTS.Transit", (c, p) => c.Get(RiseTransitSet, p.Number).Transit)
                .WithFormatter(Formatters.Time);

            e.Add("RTS.Set", (c, p) => c.Get(RiseTransitSet, p.Number).Set)
               .WithFormatter(Formatters.Time);
        }

        CelestialObjectInfo IInfoProvider<Planet>.GetInfo(SkyContext c, Planet planet)
        {
            int p = planet.Number;
            StringBuilder sb = new StringBuilder();

            sb.Append("Rise: ").Append(Formatters.Time.Format(c.Get(RiseTransitSet, p).Rise)).AppendLine();
            sb.Append("Transit: ").Append(Formatters.Time.Format(c.Get(RiseTransitSet, p).Transit)).AppendLine();
            sb.Append("Set: ").Append(Formatters.Time.Format(c.Get(RiseTransitSet, p).Set)).AppendLine();

            return null;
        }
    }
}