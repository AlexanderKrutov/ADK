﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.Math;
using static Astrarium.Algorithms.Angle;

[assembly: InternalsVisibleTo("Astrarium.Algorithms.Tests")]

namespace Astrarium.Algorithms
{
    /// <summary>
    /// Contains methods for calculating solar eclpses
    /// </summary>
    public static class SolarEclipses
    {
        /// <summary>
        /// Finds polynomial Besselian elements by 5 positions of Sun and Moon
        /// </summary>
        /// <param name="positions">Positions of Sun and Moon</param>
        /// <returns>Polynomial Besselian elements of the Solar eclipse</returns>
        public static PolynomialBesselianElements FindPolynomialBesselianElements(SunMoonPosition[] positions)
        {
            if (positions.Length != 5)
                throw new ArgumentException("Five positions are required", nameof(positions));

            double step = positions[1].JulianDay - positions[0].JulianDay;

            if (!positions.Zip(positions.Skip(1), 
                (a, b) => new { a, b })
                .All(p => Abs(p.b.JulianDay - p.a.JulianDay - step) <= 1e-6))
            {
                throw new ArgumentException("Positions should be sorted ascending by JulianDay value, and have same JulianDay step.", nameof(positions));
            }                

            // 5 time instants required
            InstantBesselianElements[] elements = new InstantBesselianElements[5];

            PointF[] points = new PointF[5];
            for (int i = 0; i < 5; i++)
            {
                elements[i] = FindInstantBesselianElements(positions[i]);
                points[i].X = i - 2;
            }

            // Mu expressed in degrees and can cross zero point.
            // Values must be aligned in order to avoid crossing.
            double[] Mu = elements.Select(e => e.Mu).ToArray();
            Angle.Align(Mu);
            for (int i = 0; i < 5; i++)
            {
                elements[i].Mu = Mu[i];      
            }

            return new PolynomialBesselianElements()
            {
                JulianDay0 = positions[2].JulianDay,
                DeltaT = Date.DeltaT(positions[2].JulianDay),
                Step = step,
                X = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].X)), 3),
                Y = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].Y)), 3),
                L1 = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].L1)), 3),
                L2 = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].L2)), 3),
                D = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].D)), 3),
                Mu = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].Mu)), 3),                
                F1 = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].F1)), 3),
                F2 = LeastSquares.FindCoeffs(points.Select((p, i) => new PointF(p.X, (float)elements[i].F2)), 3)
            };
        }

        /// <summary>
        /// Calculates Besselian elements for solar eclipse,
        /// valid only for specified instant.
        /// </summary>
        /// <param name="position">Sun and Moon position data</param>
        /// <returns>
        /// Besselian elements for solar eclipse
        /// </returns>
        /// <remarks>
        /// The method is based on formulae given here:
        /// https://de.wikipedia.org/wiki/Besselsche_Elemente
        /// </remarks>
        internal static InstantBesselianElements FindInstantBesselianElements(SunMoonPosition position)
        {
            // Nutation elements
            var nutation = Nutation.NutationElements(position.JulianDay);

            // True obliquity
            var epsilon = Date.TrueObliquity(position.JulianDay, nutation.deltaEpsilon);

            // Greenwich apparent sidereal time 
            double theta = Date.ApparentSiderealTime(position.JulianDay, nutation.deltaPsi, epsilon);

            double aSun = ToRadians(position.Sun.Alpha);
            double dSun = ToRadians(position.Sun.Delta);

            double aMoon = ToRadians(position.Moon.Alpha);
            double dMoon = ToRadians(position.Moon.Delta);

            // Earth->Sun vector
            var Rs = new Vector(
                position.DistanceSun * Cos(aSun) * Cos(dSun),
                position.DistanceSun * Sin(aSun) * Cos(dSun),
                position.DistanceSun * Sin(dSun)
            );

            // Earth->Moon vector
            var Rm = new Vector(
                position.DistanceMoon * Cos(aMoon) * Cos(dMoon),
                position.DistanceMoon * Sin(aMoon) * Cos(dMoon),
                position.DistanceMoon * Sin(dMoon)
            );

            Vector Rsm = Rs - Rm;

            double lenRsm = Vector.Norm(Rsm);

            // k vector
            Vector k = Rsm / lenRsm;

            double d = Asin(k.Z);
            double a = Atan2(k.Y, k.X);

            double x = position.DistanceMoon * Cos(dMoon) * Sin(aMoon - a);
            double y = position.DistanceMoon * (Sin(dMoon) * Cos(d) - Cos(dMoon) * Sin(d) * Cos(aMoon - a));
            double zm = position.DistanceMoon * (Sin(dMoon) * Sin(d) + Cos(dMoon) * Cos(d) * Cos(aMoon - a));

            // Sun and Moon radii, in Earth equatorial radii
            //
            // Values are taken from "Astronomy on the PC" book, 
            // Oliver Montenbruck, Thomas Pfleger, 
            // Russian edition, p. 189.
            double rhoSun = 218.25 / 2;
            double rhoMoon = 0.5450 / 2;

            double sinF1 = (rhoSun + rhoMoon) / lenRsm;
            double sinF2 = (rhoSun - rhoMoon) / lenRsm;

            double F1 = Asin(sinF1);
            double F2 = Asin(sinF2);

            double zv1 = zm + rhoMoon / sinF1;
            double zv2 = zm - rhoMoon / sinF2;

            double l1 = zv1 * Tan(F1);
            double l2 = zv2 * Tan(F2);

            return new InstantBesselianElements()
            {
                X = x,
                Y = y,
                L1 = l1,
                L2 = l2,
                D = ToDegrees(d),
                Mu = To360(theta - ToDegrees(a)),
                F1 = ToDegrees(F1),
                F2 = ToDegrees(F2)
            };
        }

        public static SolarEclipsePoint GetEclipseCurvePoint(PolynomialBesselianElements pbe, CrdsGeographical g, int i = 0, double G = 0)
        {
            // Sanity checks:
            if (!(i == 0 || i == -1 || i == 1))
                throw new ArgumentException($"Invalid value of {nameof(i)} argument", nameof(i));
            if (G < 0 || G > 1)
                throw new ArgumentException($"Invalid value of {nameof(G)} argument", nameof(G));

            double deltaT = pbe.DeltaT ?? Date.DeltaT(pbe.JulianDay0);

            double t = 0;   // time since jd0
            double phi = g.Latitude; // latitude

            double tau;
            double deltaPhi;

            int iters = 0;

            // Earth flattening
            const double flat = 1.0 / 298.257;

            // Earth flattening constant, used in calculation: 0.99664719
            double fconst = 1.0 - flat;

            double jd;

            double d, H;

            do
            {
                iters++;

                jd = pbe.JulianDay0 + t * pbe.Step;
                var be = pbe.GetInstantBesselianElements(jd);

                double X = be.X;
                double Y = be.Y;
                d = ToRadians(be.D);
                double M = be.Mu;
                H = ToRadians(M - g.Longitude - 0.00417807 * deltaT);


                double dX = be.dX; 
                double dY = be.dY;

                

                double U = Atan(fconst * Tan(ToRadians(phi)));

                double rhoSinPhi_ = fconst * Sin(U);
                double rhoCosPhi_ = Cos(U);

                double ksi = rhoCosPhi_ * Sin(H);
                double eta = rhoSinPhi_ * Cos(d) - rhoCosPhi_ * Cos(H) * Sin(d);
                double zeta = rhoSinPhi_ * Sin(d) + rhoCosPhi_ * Cos(H) * Cos(d);

                double ksi_ = ToRadians(pbe.Mu[1] * rhoCosPhi_ * Cos(H));
                double eta_ = ToRadians(pbe.Mu[1] * ksi * Sin(d) - zeta * pbe.D[1]);

                double u = X - ksi;
                double v = Y - eta;

                double a = dX - ksi_;
                double b = dY - eta_;

                double n2 = a * a + b * b;

                double n = Sqrt(n2);

                tau = -(u * a + v * b) / n2;

                double W = (v * a - u * b) / n;

                double Q1 = b * Sin(H) * rhoSinPhi_;
                double Q2 = a * (Cos(H) * Sin(d) * rhoSinPhi_ + Cos(d) * rhoCosPhi_);

                double Q = (Q1 + Q2) / ToDegrees(n);

                double dL1 = be.L1 - zeta * pbe.tanF1;
                double dL2 = be.L2 - zeta * pbe.tanF2;

                double E = dL1 - G * (dL1 + dL2);

                deltaPhi = (W + i * Abs(E)) / Q;

                t += tau;
                phi += deltaPhi;

                phi = ToDegrees(Asin(Sin(ToRadians(phi))));

            }
            while ((Abs(tau) >= 0.0001 || Abs(deltaPhi) >= 0.0001) && iters < 20);

            if (Abs(phi) > 90)
            {
                return null;
            }

            double sinh = Sin(d) * Sin(ToRadians(phi)) + Cos(d) * Cos(ToRadians(phi)) * Cos(H);

            if (sinh < 0)
            {
                // Sun is below horizon
                return null;
            }

            if (Abs(tau) < 0.0001 && Abs(deltaPhi) < 0.0001)
            {
                return new SolarEclipsePoint(jd, new CrdsGeographical(g.Longitude, phi));
            }
            else
            {
                //return new SolarEclipsePoint(jd, new CrdsGeographical(g.Longitude, phi));
                // no point
                //return null;
                return null;
            }
        }


        /// <summary>
        /// Gets map of solar eclipse.
        /// </summary>
        /// <param name="pbe">Polynomial Besselian elements defining the Eclipse</param>
        /// <returns><see cref="SolarEclipseMap"/> instance.</returns>
        public static SolarEclipseMap GetEclipseMap(PolynomialBesselianElements pbe, SolarEclipseType eclipseType)
        {
            // left edge of time interval
            double jdFrom = pbe.From;

            // midpoint of time interval
            double jdMid = pbe.From + (pbe.To - pbe.From) / 2;

            // right edge of time interval
            double jdTo = pbe.To;

            // precision of calculation, in days
            double epsilon = 1e-8;

            // Eclipse map data
            SolarEclipseMap map = new SolarEclipseMap();

            // Function has zero value when umbra center crosses Earth edge
            Func<double, double> funcUmbra = (jd) =>
            {
                var b = pbe.GetInstantBesselianElements(jd);
                return Sqrt(b.X * b.X + b.Y * b.Y) - 1 - Abs(b.L2);
            };

            // Function has zero value when penumbra edge crosses Earth edge externally
            Func<double, double> funcExternalContact = (jd) =>
            {
                var b = pbe.GetInstantBesselianElements(jd);
                return Sqrt(b.X * b.X + b.Y * b.Y) - 1 - b.L1;
            };

            // Function has zero value when penumbra edge crosses Earth edge internally
            Func<double, double> funcInternalContact = (jd) =>
            {
                var b = pbe.GetInstantBesselianElements(jd);
                return Sqrt(b.X * b.X + b.Y * b.Y) - 1 + b.L1;
            };

            // Function has zero value when northern limit of umbra crosses Earth edge
            Func<double, double> funcUmbraNorthLimit = (jd) =>
            {
                var b = pbe.GetInstantBesselianElements(jd);
                double angle = ToRadians(b.Inc + 90);
                var p = new PointF(
                        (float)(b.X + Abs(b.L2) * Cos(angle)),
                        (float)(b.Y + Abs(b.L2) * Sin(angle)));

                return Sqrt(p.X * p.X + p.Y * p.Y) - 1;
            };

            // Function has zero value when southern limit of umbra crosses Earth edge
            Func<double, double> funcUmbraSouthLimit = (jd) =>
            {
                var b = pbe.GetInstantBesselianElements(jd);
                double angle = ToRadians(b.Inc - 90);
                var p = new PointF(
                        (float)(b.X + Abs(b.L2) * Cos(angle)),
                        (float)(b.Y + Abs(b.L2) * Sin(angle)));

                return Sqrt(p.X * p.X + p.Y * p.Y) - 1;
            };

            // Instant of first external contact of penumbra,
            // assume always exists
            double jdP1 = FindRoots(funcExternalContact, jdFrom, jdMid, epsilon);
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jdP1);
                double a = Atan2(b.Y, b.X);
                PointF p = new PointF((float)Cos(a), (float)Sin(a));                
                map.P1 = new SolarEclipsePoint(jdP1, ProjectOnEarth(p, b.D, b.Mu, true));
            }

            // Instant of last external contact of penumbra
            // assume always exists
            double jdP4 = FindRoots(funcExternalContact, jdMid, jdTo, epsilon);
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jdP4);
                double a = Atan2(b.Y, b.X);
                PointF p = new PointF((float)Cos(a), (float)Sin(a));
                map.P4 = new SolarEclipsePoint(jdP4, ProjectOnEarth(p, b.D, b.Mu, true));
            }

            // Instant of first internal contact of penumbra,
            // may not exist
            double jdP2 = FindRoots(funcInternalContact, jdFrom, jdMid, epsilon);
            if (!double.IsNaN(jdP2))
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jdP2);
                double a = Atan2(b.Y, b.X);
                PointF p = new PointF((float)Cos(a), (float)Sin(a));
                map.P2 = new SolarEclipsePoint(jdP2, ProjectOnEarth(p, b.D, b.Mu, true));
            }

            // Instant of last internal contact of penumbra,
            // may not exist
            double jdP3 = FindRoots(funcInternalContact, jdMid, jdTo, epsilon);
            if (!double.IsNaN(jdP3))
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jdP3);
                double a = Atan2(b.Y, b.X);
                PointF p = new PointF((float)Cos(a), (float)Sin(a));
                map.P3 = new SolarEclipsePoint(jdP3, ProjectOnEarth(p, b.D, b.Mu, true));
            }

            // Instant when northern limit of umbra crosses Earth edge first time,
            // may not exist
            double jdUN1 = FindRoots(funcUmbraNorthLimit, jdFrom, jdMid, epsilon);
            
            // Instant when northern limit of umbra crosses Earth edge last time,
            // may not exist
            double jdUN2 = FindRoots(funcUmbraNorthLimit, jdMid, jdTo, epsilon);
            
            // Instant when southern limit of umbra crosses Earth edge first time,
            // may not exist
            double jdUS1 = FindRoots(funcUmbraSouthLimit, jdFrom, jdMid, epsilon);
           
            // Instant when southern limit of umbra crosses Earth edge last time,
            // may not exist
            double jdUS2 = FindRoots(funcUmbraSouthLimit, jdMid, jdTo, epsilon);

            // Instant of first contact of umbra center,
            // may not exist
            double jdC1 = FindRoots(funcUmbra, jdFrom, jdMid, epsilon);
            if (!double.IsNaN(jdC1))
            {
                do
                {
                    InstantBesselianElements b = pbe.GetInstantBesselianElements(jdC1);
                    PointF p = new PointF((float)b.X, (float)b.Y);
                    var g = ProjectOnEarth(p, b.D, b.Mu);
                    if (g == null) 
                        jdC1 += 1e-6;
                    else
                        map.C1 = new SolarEclipsePoint(jdC1, g);
                }
                while (map.C1 == null && jdC1 < jdMid);
            }

            // Instant of last contact of umbra center,
            // may not exist
            double jdC2 = FindRoots(funcUmbra, jdMid, jdTo, epsilon);
            if (!double.IsNaN(jdC2))
            {
                do
                {
                    InstantBesselianElements b = pbe.GetInstantBesselianElements(jdC2);
                    PointF p = new PointF((float)b.X, (float)b.Y);
                    var g = ProjectOnEarth(p, b.D, b.Mu);
                    if (g == null)
                        jdC2 -= 1e-6;
                    else
                        map.C2 = new SolarEclipsePoint(jdC2, g);
                }
                while (map.C2 == null && jdC2 > jdMid);
            }

            // Instant of eclipse maximum
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(pbe.JulianDay0);
                PointF p = new PointF((float)b.X, (float)b.Y);
                                              
                var g = ProjectOnEarth(p, b.D, b.Mu, true);
                if (g != null)
                {
                    map.Max = new SolarEclipsePoint(pbe.JulianDay0, g);
                }
            }

            // initial longitude to start iteration process
            // to find map's curves points


            var beMax = pbe.GetInstantBesselianElements(map.Max.JulianDay);

            // TOTAL PATH by Meeus algorithm:

            if (eclipseType != SolarEclipseType.Partial)
            {
                double lambda0 = 0;

                var points = FindCurvePoints(pbe, lambda0, 0, 0);

                //CrdsGeographical g = points.FirstOrDefault().Coordinates;
                //do
                //{
                //    int index = map.TotalPath.IndexOf(g);
                //    if (index + 1 < points.Count)
                //    {
                //        var gNext = points[index + 1].Coordinates;



                //        if (count > 1)
                //        {
                //            var pts = new List<CrdsGeographical>();
                //            for (int i = 0; i < count; i++)
                //            {
                //                var m = Intermediate(g, gNext, (double)i / count);


                //                var m1 = GetEclipseCurvePoint(pbe, new CrdsGeographical(m), 0, 0);
                //                if (m1 != null)
                //                {
                //                    pts.Add(m1.Coordinates);

                //                    //pts.Add(m2.Coordinates);
                //                }
                //                else
                //                {
                //                    pts.Add(m);
                //                }
                //            }
                //            map.TotalPath.InsertRange(index + 1, pts);
                //        }
                //        g = gNext;
                //    }
                //    else
                //    {
                //        break;
                //    }
                //}
                //while (true);

                map.TotalPath = Order(points);// .Select(p => p.Coordinates).ToList();
            }

            if (eclipseType != SolarEclipseType.Partial)
            {
                double lambda0 = 0;// map.Max.Coordinates.Longitude;

                var northPoints = FindCurvePoints(pbe, lambda0, 1, 1);
                var northPointsOrdered = Order(northPoints);

                var southPoints = FindCurvePoints(pbe, lambda0, -1, 1);
                var southPointsOrdered = Order(southPoints);


                if (northPoints.Count > 2)
                {
                    var m1 = northPointsOrdered.OrderByDescending(p => Abs(p.Latitude)).First();
                    int northIndex = northPointsOrdered.IndexOf(m1);

                    map.UmbraNorthernLimit[0] = northPointsOrdered.Take(northIndex).ToList();
                    map.UmbraNorthernLimit[1] = northPointsOrdered.Skip(northIndex - 1).ToList();
                }

                if (southPoints.Count > 2)
                {
                    var m2 = southPointsOrdered.OrderByDescending(p => Abs(p.Latitude)).First();
                    int southIndex = southPointsOrdered.IndexOf(m2);

                    map.UmbraSouthernLimit[0] = southPointsOrdered.Take(southIndex).ToList();
                    map.UmbraSouthernLimit[1] = southPointsOrdered.Skip(southIndex - 1).ToList();
                }
            }


            // Find points of Northern limit of penumbra
            {
                //double ang = Atan2(beMax.Y, beMax.X);
                //float x = (float)(beMax.X + beMax.L1 * Cos(ang));
                //float y = (float)(beMax.Y + beMax.L1 * Sin(ang));
                //var p = ProjectOnEarth(new PointF(x, y), beMax.D, beMax.Mu);

                //if (p != null)
                {
                    double lambda0 = 0;// -69;

                    var points = FindCurvePoints(pbe, lambda0, 1, 0);
                    map.PenumbraNorthernLimit = Order(points);
                }
            }

            // Find points of Southern limit of penumbra 
            {
                //double ang = Atan2(beMax.Y, beMax.X);
                //float x = (float)(beMax.X + beMax.L1 * Cos(ang));
                //float y = (float)(beMax.Y - beMax.L1 * Sin(ang));
                //var p = ProjectOnEarth(new PointF(x, y), beMax.D, beMax.Mu);

                //if (p != null)
                //{
                //    double lambda0 = p.Longitude;

                    var points = FindCurvePoints(pbe, 0, -1, 0);
                    map.PenumbraSouthernLimit = Order(points);
                //}
            }

            // Calc rise/set curves
            FindRiseSetCurves(pbe, map, jdP1, jdP4);

            return map;
        }

        private static List<CrdsGeographical> Order(List<SolarEclipsePoint> points)
        {
            if ( points.Count <= 2)
            {
                return points.Select(p => p.Coordinates).ToList();
            }

            double jdMin = points.Min(p => p.JulianDay);
            double jdMax = points.Max(p => p.JulianDay);

            double jdMid = (jdMin + jdMax) / 2;

            var mid = points.OrderBy(p => Abs(p.JulianDay - jdMid)).First().Coordinates;

            var jdOrdered = points.OrderBy(p => p.JulianDay).Select(p => p.Coordinates).ToList();

            int middleIndex = jdOrdered.IndexOf(mid);

            var left = jdOrdered.Take(middleIndex);
            var right = jdOrdered.Skip(middleIndex + 1);

            var newList = new List<CrdsGeographical>();

            newList.AddRange(left.OrderByDescending(p => Separation(mid, p)));
            newList.Add(mid);
            newList.AddRange(right.OrderBy(p => Separation(mid, p)));

            return newList.ToList();
        }


        private static List<CrdsGeographical> FindTotalPathByTime(PolynomialBesselianElements pbe)
        {
            double deltaT = pbe.DeltaT ?? Date.DeltaT(pbe.JulianDay0);

            List<CrdsGeographical> points = new List<CrdsGeographical>();

            for (double jd = pbe.JulianDay0 - 0.5; jd< pbe.JulianDay0 + 0.5; jd += TimeSpan.FromMinutes(1).TotalDays)
            {
                var be = pbe.GetInstantBesselianElements(jd);
                double X = be.X;
                double Y = be.Y;
                double d = ToRadians(be.D);
                double M = be.Mu;

                double omega = 1.0 / Sqrt(1 - 0.006_694_385 * Cos(d) * Cos(d));

                double y1 = omega * Y;
                double b1 = omega * Sin(d);
                double b2 = 0.996_647_19 * omega * Cos(d);

                double B2 = 1 - X * X - y1 * y1;

                if (B2 >= 0)
                {
                    double B = Sqrt(B2);

                    double H = ToDegrees(Atan2(X, B * b2 - y1 * b1));

                    double phi1 = Asin(B * b1 + y1 * b2);

                    double phi = ToDegrees(Atan(1.003_364_09 * Tan(phi1)));

                    double lambda = M - H - 0.004_178_07 * deltaT;

                    points.Add(new CrdsGeographical(lambda, phi));
                }
            }

            return points;
        }

        internal static double FindFunctionEnd(Func<double, bool> func, double left, double right, bool leftExist, bool rightExist, double epsilon)
        {
            double mid = (left + right) / 2;
            bool midExist = func(mid);

            if (Abs(left - right) < epsilon)
            {
                return leftExist ? left : right;
            }

            if (leftExist && !rightExist)
            {
                // 1
                if (midExist)
                {
                    return FindFunctionEnd(func, mid, right, true, false, epsilon);
                }
                // 2
                else
                {
                    return FindFunctionEnd(func, left, mid, true, false, epsilon);
                }
            }
            else if (!leftExist && rightExist)
            {
                // 3
                if (midExist)
                {
                    return FindFunctionEnd(func, left, mid, false, true, epsilon);
                }
                // 4
                else
                {
                    return FindFunctionEnd(func, mid, right, false, true, epsilon);
                }
            }
            else 
            {
                throw new Exception();
            }
        } 

        private static List<SolarEclipsePoint> FindCurvePoints(PolynomialBesselianElements pbe, double lambda0, int i, int G)
        {
            double[] phis = new double[] { 0, Sign(pbe.Y[0]) * 89.9 };           
            bool[] prevExist = new bool[2];
            var points = new List<SolarEclipsePoint>();

            double step = 1;

            for (double lambda = lambda0 - 180; lambda <= lambda0 + 180; lambda += step)
            {
                for (int k = 0; k < 2; k++)
                {
                    SolarEclipsePoint p = GetEclipseCurvePoint(pbe, new CrdsGeographical(lambda, phis[k]), i, G);

                    bool exist = p != null;

                    if (prevExist[k] != exist)
                    {
                        Func<double, bool> func = (lon) => GetEclipseCurvePoint(pbe, new CrdsGeographical(lon, phis[k]), i, G) != null;
                        double lon0 = FindFunctionEnd(func, lambda - step, lambda, prevExist[k], exist, 0.0001);
                        var p0 = GetEclipseCurvePoint(pbe, new CrdsGeographical(lon0, phis[k]), i, G);
                        if (p0 != null)
                        {
                            points.Add(p0);
                        }
                    }

                    if (prevExist[k] && exist)
                    {
                        //double sep = Separation(points.Last().Coordinates, p.Coordinates);
                        //if (sep > 1)
                        //{
                        //    step = 1 / sep;
                        //}
                    }

                    if (p != null)
                    {
                        points.Add(p);
                    }

                    prevExist[k] = exist;
                }
            }

            return points;
        }


        private class CurvePoint
        {
            public double Xi { get; set; }
            public CrdsGeographical Location { get; set; }
        }

        private static void FindPenumbraLimits(PolynomialBesselianElements pbe, IList<CrdsGeographical> curve, double jdFrom, double jdTo, double ang)
        {
            List<CurvePoint> points = new List<CurvePoint>();   

            // Earth ellipticity, squared
            const double e2 = 0.00669454;

            const double epsilon = 1e-6;

            double step = FindStep(jdTo - jdFrom) * 2;

            double zeta0 = 0;
            double Q0 = 0;

            // latest values of xi and eta
            double xi0 = double.NaN;
            double eta0 = double.NaN;

            for (double jd = jdFrom; jd <= jdTo; jd += step)
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jd);

                double l = b.L1;
                double l_ = b.dL1;

                double mu_ = ToRadians(b.dMu);
                double x = b.X;
                double x_ = b.dX;
                double y = b.Y;
                double y_ = b.dY;
                double d = ToRadians(b.D);
                double d_ = ToRadians(b.dD);
                double f = ToRadians(b.F1);

                double rho1 = Sqrt(1 - e2 * Cos(d) * Cos(d));
                double rho2 = Sqrt(1 - e2 * Sin(d) * Sin(d));
                double sind1 = Sin(d) / rho1;
                double cosd1 = Sqrt(1 - e2) * Cos(d) / rho1;
                double d1 = Atan2(sind1, cosd1);
                double sind1d2 = e2 * Sin(d) * Cos(d) / (rho1 * rho2);
                double cosd1d2 = Sqrt(1 - e2) / (rho1 * rho2);

                // 8.3422-2
                double a_ = -l_ - mu_ * Tan(f) * x * Cos(d) + y * d_ * Tan(f);
                double b_ = -y_ + mu_ * x * Sin(d) + l * d_ * Tan(f);
                double c_ = x_ + mu_ * y * Sin(d) + mu_ * Tan(f) * l * Cos(d);

                // Find value of Q from equation 8.353-2:
                // a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0)) = 0
                // zeta0 is a previous value of zeta.
                // Q0 is a previous value of Q.
                // Start with Q0 = 0 and zeta0 = 0.
                // Now need to find roots of the equation.
                // But instead of algorithm in art. 8.3554, we solve it in another way.
                // The equation has form:
                // a*sin(x) + b*cos(x) = c
                // It can be found with method described there: 
                // https://socratic.org/questions/59e5f259b72cff6c4402a6a5

                double Q = 0;

                // iterate thru the roots
                for (int k = 0; k < 2; k++)
                {
                    Q = Pow(-1, k) * Asin((-a_ - zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0))) / Sqrt(b_ * b_ + c_ * c_)) - Atan2(-b_, c_) + PI * k;
                    if (l * Cos(Q) > 0 && ang == -90) break;
                    if (l * Cos(Q) < 0 && ang == 90) break;
                }

                // remember found value of Q
                Q0 = Q;

                double eq0;
                double eq = 0;
                double xi, eta1, zeta1;
                double zeta = 0;
                double L;

                double zeta1_2;

                // find coordinates on fundamental plane
                do
                {
                    eq0 = eq;
                    L = l - zeta * Tan(f);
                    xi = x - L * Sin(Q);
                    eta1 = (y - L * Cos(Q)) / rho1;

                    zeta1_2 = 1 - xi * xi - eta1 * eta1;
                    zeta1 = Sqrt(zeta1_2);

                    if (zeta1_2 < 0 && zeta1_2 > -0.0125)
                    {
                        zeta1 = 0;
                        double P = Atan2(eta1, xi);
                        xi = Cos(P);
                        eta1 = Sin(P);
                    }

                    // 8.3554-1
                    zeta = rho2 * (zeta1 * cosd1d2 - eta1 * sind1d2);

                    // 8.353-2
                    eq = a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q) - mu_ * Cos(d) * Sin(Q));

                    // remember last value of zeta
                    zeta0 = double.IsNaN(zeta) ? 0 : zeta;
                }
                while (Abs(eq) > epsilon && Abs(eq0 - eq) > epsilon);

                CurvePoint p = new CurvePoint();

                if (xi < xi0 || (y_ > 0 && eta1 < eta0) || (y_ < 0 && eta1 > eta0))
                {
                    p = points.OrderBy(c => Abs(xi - c.Xi)).FirstOrDefault();                    
                }
                else
                {
                    xi0 = xi;
                    eta0 = eta1;
                    p = new CurvePoint();
                    points.Add(p);
                }

                // 8.333-13
                var v = Matrix.R1(d1) * new Vector(xi, eta1, zeta1);

                double phi1 = Asin(v.Y);
                double sinTheta = v.X / Cos(phi1);
                double cosTheta = v.Z / Cos(phi1);

                double theta = ToDegrees(Atan2(sinTheta, cosTheta));
                double tanPhi = Tan(phi1) / Sqrt(1 - e2);
                double phi = Atan(tanPhi);

                // 8.331-4
                double lambda = b.Mu - theta;

                p.Xi = xi;
                p.Location = new CrdsGeographical(To360(lambda + 180) - 180, ToDegrees(phi));
            }

            foreach (var p in points)
            {
                curve.Add(p.Location);
            }

        }

        private static double FindPenumbraLongitude(PolynomialBesselianElements pbe, double jd, int ang)
        {
            List<CurvePoint> points = new List<CurvePoint>();

            // Earth ellipticity, squared
            const double e2 = 0.00669454;

            const double epsilon = 1e-6;

            double zeta0 = 0;
            double Q0 = 0;

            // latest values of xi and eta
            double xi0 = double.NaN;
            double eta0 = double.NaN;

            
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jd);

                double l = b.L1;
                double l_ = b.dL1;

                double mu_ = ToRadians(b.dMu);
                double x = b.X;
                double x_ = b.dX;
                double y = b.Y;
                double y_ = b.dY;
                double d = ToRadians(b.D);
                double d_ = ToRadians(b.dD);
                double f = ToRadians(b.F1);

                double rho1 = Sqrt(1 - e2 * Cos(d) * Cos(d));
                double rho2 = Sqrt(1 - e2 * Sin(d) * Sin(d));
                double sind1 = Sin(d) / rho1;
                double cosd1 = Sqrt(1 - e2) * Cos(d) / rho1;
                double d1 = Atan2(sind1, cosd1);
                double sind1d2 = e2 * Sin(d) * Cos(d) / (rho1 * rho2);
                double cosd1d2 = Sqrt(1 - e2) / (rho1 * rho2);

                // 8.3422-2
                double a_ = -l_ - mu_ * Tan(f) * x * Cos(d) + y * d_ * Tan(f);
                double b_ = -y_ + mu_ * x * Sin(d) + l * d_ * Tan(f);
                double c_ = x_ + mu_ * y * Sin(d) + mu_ * Tan(f) * l * Cos(d);

                // Find value of Q from equation 8.353-2:
                // a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0)) = 0
                // zeta0 is a previous value of zeta.
                // Q0 is a previous value of Q.
                // Start with Q0 = 0 and zeta0 = 0.
                // Now need to find roots of the equation.
                // But instead of algorithm in art. 8.3554, we solve it in another way.
                // The equation has form:
                // a*sin(x) + b*cos(x) = c
                // It can be found with method described there: 
                // https://socratic.org/questions/59e5f259b72cff6c4402a6a5

                double Q = 0;

                // iterate thru the roots
                for (int k = 0; k < 2; k++)
                {
                    Q = Pow(-1, k) * Asin((-a_ - zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0))) / Sqrt(b_ * b_ + c_ * c_)) - Atan2(-b_, c_) + PI * k;
                    if (l * Cos(Q) > 0 && ang == -90) break;
                    if (l * Cos(Q) < 0 && ang == 90) break;
                }

                // remember found value of Q
                Q0 = Q;

                double eq0;
                double eq = 0;
                double xi, eta1, zeta1;
                double zeta = 0;
                double L;

                double zeta1_2;

                // find coordinates on fundamental plane
                do
                {
                    eq0 = eq;
                    L = l - zeta * Tan(f);
                    xi = x - L * Sin(Q);
                    eta1 = (y - L * Cos(Q)) / rho1;

                    zeta1_2 = 1 - xi * xi - eta1 * eta1;
                    zeta1 = Sqrt(zeta1_2);

                    if (zeta1_2 < 0 && zeta1_2 > -0.0125)
                    {
                        zeta1 = 0;
                        double P = Atan2(eta1, xi);
                        xi = Cos(P);
                        eta1 = Sin(P);
                    }

                    // 8.3554-1
                    zeta = rho2 * (zeta1 * cosd1d2 - eta1 * sind1d2);

                    // 8.353-2
                    eq = a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q) - mu_ * Cos(d) * Sin(Q));

                    // remember last value of zeta
                    zeta0 = double.IsNaN(zeta) ? 0 : zeta;
                }
                while (Abs(eq) > epsilon && Abs(eq0 - eq) > epsilon);

                CurvePoint p = new CurvePoint();

                if (xi < xi0 || (y_ > 0 && eta1 < eta0) || (y_ < 0 && eta1 > eta0))
                {
                    p = points.OrderBy(c => Abs(xi - c.Xi)).FirstOrDefault();
                }
                else
                {
                    xi0 = xi;
                    eta0 = eta1;
                    p = new CurvePoint();
                    points.Add(p);
                }

                // 8.333-13
                var v = Matrix.R1(d1) * new Vector(xi, eta1, zeta1);

                double phi1 = Asin(v.Y);
                double sinTheta = v.X / Cos(phi1);
                double cosTheta = v.Z / Cos(phi1);

                double theta = ToDegrees(Atan2(sinTheta, cosTheta));
                double tanPhi = Tan(phi1) / Sqrt(1 - e2);
                double phi = Atan(tanPhi);

                // 8.331-4
                double lambda = b.Mu - theta;

                p.Xi = xi;

                return To360(lambda + 180) - 180;

                //p.Location = new CrdsGeographical(To360(lambda + 180) - 180, ToDegrees(phi));
            }



        }




        private static void FindUmbraLimits(PolynomialBesselianElements pbe, ICollection<CrdsGeographical>[] curve, double jdFrom, double jdTo, double ang)
        {
            if (!double.IsNaN(jdFrom) && !double.IsNaN(jdTo))
            {
                int c = 0;

                // Earth ellipticity, squared
                const double e2 = 0.00669454;

                const double epsilon = 1e-6;

                double step = FindStep(jdTo - jdFrom);

                double zeta0 = 0;
                double Q0 = 0;

                for (double jd = jdFrom; jd <= jdTo; jd += step)
                {
                    InstantBesselianElements b = pbe.GetInstantBesselianElements(jd);

                    double l = b.L2;
                    double l_ = b.dL2;

                    double mu_ = ToRadians(b.dMu);
                    double x = b.X;
                    double x_ = b.dX;
                    double y = b.Y;
                    double y_ = b.dY;
                    double d = ToRadians(b.D);
                    double d_ = ToRadians(b.dD);
                    double f = ToRadians(b.F2);

                    double rho1 = Sqrt(1 - e2 * Cos(d) * Cos(d));
                    double rho2 = Sqrt(1 - e2 * Sin(d) * Sin(d));
                    double sind1 = Sin(d) / rho1;
                    double cosd1 = Sqrt(1 - e2) * Cos(d) / rho1;
                    double d1 = Atan2(sind1, cosd1);
                    double sind1d2 = e2 * Sin(d) * Cos(d) / (rho1 * rho2);
                    double cosd1d2 = Sqrt(1 - e2) / (rho1 * rho2);

                    // 8.3422-2
                    double a_ = -l_ - mu_ * Tan(f) * x * Cos(d) + y * d_ * Tan(f);
                    double b_ = -y_ + mu_ * x * Sin(d) + l * d_ * Tan(f);
                    double c_ = x_ + mu_ * y * Sin(d) + mu_ * Tan(f) * l * Cos(d);

                    // Find value of Q from equation 8.353-2:
                    // a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0)) = 0
                    // zeta0 is a previous value of zeta.
                    // Q0 is a previous value of Q.
                    // Start with Q0 = 0 and zeta0 = 0.
                    // Now need to find roots of the equation.
                    // But instead of algorithm in art. 8.3554, we solve it in another way.
                    // The equation has form:
                    // a*sin(x) + b*cos(x) = c
                    // It can be found with method described there: 
                    // https://socratic.org/questions/59e5f259b72cff6c4402a6a5

                    double Q = 0;

                    // iterate thru the roots
                    for (int k = 0; k < 2; k++)
                    {
                        Q = Pow(-1, k) * Asin((-a_ - zeta0 * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q0) - mu_ * Cos(d) * Sin(Q0))) / Sqrt(b_ * b_ + c_ * c_)) - Atan2(-b_, c_) + PI * k;
                        if (l * Cos(Q) > 0 && ang == -90) break;
                        if (l * Cos(Q) < 0 && ang == 90) break;
                    }

                    // remember found value of Q
                    Q0 = Q;

                    double eq0;
                    double eq = 0;
                    double xi, eta1, zeta1;
                    double zeta = 0;
                    double L;

                    double zeta1_2;

                    // find coordinates on fundamental plane
                    do
                    {
                        eq0 = eq;
                        L = l - zeta * Tan(f);
                        xi = x - L * Sin(Q);
                        eta1 = (y - L * Cos(Q)) / rho1;

                        zeta1_2 = 1 - xi * xi - eta1 * eta1;
                        zeta1 = Sqrt(zeta1_2);

                        if (zeta1_2 < 0 && zeta1_2 > -0.00625)
                        {
                            zeta1 = 0;
                            xi = Cos(Atan2(eta1, xi));
                            eta1 = Sin(Atan2(eta1, xi));
                        }

                        // 8.3554-1
                        zeta = rho2 * (zeta1 * cosd1d2 - eta1 * sind1d2);

                        // 8.353-2
                        eq = a_ - b_ * Cos(Q) + c_ * Sin(Q) + zeta * (1 + Tan(f) * Tan(f)) * (d_ * Cos(Q) - mu_ * Cos(d) * Sin(Q));

                        // remember last value of zeta
                        zeta0 = double.IsNaN(zeta) ? 0 : zeta;
                    }
                    while (Abs(eq) > epsilon && Abs(eq0 - eq) > epsilon);

                    // 8.333-13
                    var v = Matrix.R1(d1) * new Vector(xi, eta1, zeta1);

                    double phi1 = Asin(v.Y);
                    double sinTheta = v.X / Cos(phi1);
                    double cosTheta = v.Z / Cos(phi1);

                    double theta = ToDegrees(Atan2(sinTheta, cosTheta));
                    double tanPhi = Tan(phi1) / Sqrt(1 - e2);
                    double phi = Atan(tanPhi);

                    // 8.331-4
                    double lambda = b.Mu - theta;

                    var g = new CrdsGeographical(To360(lambda + 180) - 180, ToDegrees(phi));

                    if (!double.IsNaN(g.Longitude) && !double.IsNaN(g.Latitude))
                    {
                        if (c == 0 && Abs(g.Latitude) > 85.5)
                        {
                            curve[0].Add(g);
                            c = 1;
                        }
                        curve[c].Add(g);
                    }
                }
            }
        }

        private static void FindRiseSetCurves(PolynomialBesselianElements pbe, SolarEclipseMap map, double jdFrom, double jdTo)
        {
            double step0 = FindStep(jdTo - jdFrom);
            double step = step0;
            int riseSet = 0;

            double deltaT = Date.DeltaT(jdFrom);

            for (double jd = jdFrom; jd <= jdTo + step * 0.1; jd += step)
            {
                InstantBesselianElements b = pbe.GetInstantBesselianElements(jd);

                // Projection of Moon shadow center on fundamental plane
                PointF pCenter = new PointF((float)b.X, (float)b.Y);

                // Find penumbra (L1 radius) intersection with
                // Earth circle on fundamental plane
                PointF[] pPenumbraIntersect = CirclesIntersection(pCenter, b.L1);

                // Adjust iteration step according to current penumbra projection
                if (Abs(b.X) < 0.15 && pPenumbraIntersect.Any(p => Abs(p.Y) > 0.95))
                {
                    step = step0 / 10;
                }
                else
                {
                    step = step0;
                }

                if (Abs(jd - map.P1.JulianDay) < step)
                {
                    CrdsGeographical g = map.P1.Coordinates;
                    if (pCenter.X <= 0)
                        map.RiseSetCurve[riseSet].Insert(0, g);
                    else
                        map.RiseSetCurve[riseSet].Add(g);
                }
                else if (map.P2 != null && Abs(jd - map.P2.JulianDay) < step)
                {
                    CrdsGeographical g = map.P2.Coordinates;
                    if (pCenter.X <= 0)
                        map.RiseSetCurve[riseSet].Insert(0, g);
                    else
                        map.RiseSetCurve[riseSet].Add(g);
                }
                else if (map.P3 != null && Abs(jd - map.P3.JulianDay) < step)
                {
                    CrdsGeographical g = map.P3.Coordinates;
                    if (pCenter.X <= 0)
                        map.RiseSetCurve[riseSet].Insert(0, g);
                    else
                        map.RiseSetCurve[riseSet].Add(g);
                }
                else if (Abs(jd - map.P4.JulianDay) < step)
                {
                    CrdsGeographical g = map.P4.Coordinates;
                    if (pCenter.X <= 0)
                        map.RiseSetCurve[riseSet].Insert(0, g);
                    else
                        map.RiseSetCurve[riseSet].Add(g);
                }
                else
                {
                    for (int i = 0; i < pPenumbraIntersect.Length; i++)
                    {
                        //CrdsGeographical g = Project(pPenumbraIntersect[i], b, deltaT);
                        CrdsGeographical g = ProjectOnEarth(pPenumbraIntersect[i], b.D, b.Mu, forceProjection: true);

                        //if (g == null) continue;

                        if (pCenter.X <= 0)
                        {
                            if (i == 0)
                                map.RiseSetCurve[riseSet].Insert(0, g);
                            else
                                map.RiseSetCurve[riseSet].Add(g);
                        }
                        else
                        {
                            if (i == 0)
                                map.RiseSetCurve[riseSet].Add(g);
                            else
                                map.RiseSetCurve[riseSet].Insert(0, g);
                        }
                    }
                }

                // Penumbra is totally inside Earth circle
                if (map.PenumbraNorthernLimit.Count > 0 &&
                    map.PenumbraSouthernLimit.Count > 0 &&
                    pCenter.X * pCenter.X + pCenter.Y * pCenter.Y < 1 &&
                    !pPenumbraIntersect.Any())
                {
                    riseSet = 1;
                }
            }
        }

        private static void FindTotalPath(PolynomialBesselianElements pbe, SolarEclipseMap curves, double jdFrom, double jdTo)
        {
            if (!double.IsNaN(jdFrom) && !double.IsNaN(jdTo))
            {
                int c = 0;

                double step0 = FindStep(jdTo - jdFrom);
                double step = step0;

                for (double jd = jdFrom; jd <= jdTo + step * 0.1; jd += step)
                {
                    InstantBesselianElements b = pbe.GetInstantBesselianElements(jd);

                    // Adjust iteration step according to current umbra projection
                    if (Abs(b.Y) > 0.8)
                    {
                        step = step0 / 4;
                    }
                    else
                    {
                        step = step0;
                    }

                    // Projection of Moon shadow center on fundamental plane
                    PointF pCenter = new PointF((float)b.X, (float)b.Y);

                    // Umbra center coordinates on Earth surface
                    CrdsGeographical g = ProjectOnEarth(pCenter, b.D, b.Mu);

                    if (g != null)
                    {
                        curves.TotalPath.Add(g);
                    }
                }
            } 
        }

        /// <summary>
        /// Finds step value (in Julian days) needed for calculating curve points.
        /// </summary>
        /// <param name="deltaJd">Time interval, in Julian days</param>
        /// <returns>Step value (in Julian days, closest to 1 minute) needed for calculating curve points.</returns>
        private static double FindStep(double deltaJd)
        {            
            int count = (int)(deltaJd / TimeSpan.FromMinutes(1).TotalDays) + 1;
            return deltaJd / count;
        }

        //internal static CrdsGeographical Project(PointF p, InstantBesselianElements be, double deltaT)
        //{
        //    double d = ToRadians(be.D);
        //    double omega2 = 1.0 / (1 - 0.006_694_385 * Cos(d) * Cos(d));

        //    double omega = Sqrt(omega2);

        //    double y1 = omega * p.Y;
        //    double b1 = omega * Sin(d);
        //    double b2 = 0.996_647_19 * omega * Cos(d);

        //    double discriminant = 1 - p.X * p.X - y1 * y1;

        //    double B = 0;
        //    if (discriminant >= 0)
        //    {
        //        B = Sqrt(discriminant);
        //    }
 
        //    double H = ToDegrees(Atan2(p.X, B * b2 - y1 * b1));

        //    double phi1 = Asin(B * b1 + y1 * b2);

        //    double tanPhi = 1.003_364_09 * Tan(phi1);

        //    double phi = ToDegrees(Atan(tanPhi));

        //    double lambda = be.Mu - H - 0.004_178_07 * deltaT;

        //    return new CrdsGeographical(lambda, phi);

        //}


        /// <summary>
        /// Project point from Besselian fundamental plane 
        /// to Earth surface and find geographical coordinates of projection
        /// </summary>
        /// <param name="p">Point on Besselian fundamental plane</param>
        /// <param name="d">Declination of Moon shadow vector, in degrees</param>
        /// <param name="mu">Hour angle of Moon shadow vector, in degrees</param>
        /// <returns>
        /// Geograhphical coordinates of a point on Earth surface, corresponding to the
        /// point on Besselian fundamental plane, or null if point is outside the Earth circle on the plane.
        /// </returns>
        /// <remarks>
        /// Formulae are taken from book
        /// Seidelmann, P. K.: Explanatory Supplement to The Astronomical Almanac, 
        /// University Science Book, Mill Valley (California), 1992,
        /// Chapter 8.3 "Solar Eclipses"
        /// https://archive.org/download/131123ExplanatorySupplementAstronomicalAlmanac/131123-explanatory-supplement-astronomical-almanac.pdf
        /// </remarks>
        internal static CrdsGeographical ProjectOnEarth(PointF p, double d, double mu, bool forceProjection = false)
        {            
            // Earth ellipticity, squared
            const double e2 = 0.00669454;

            // 8.334-1
            double rho1 = Sqrt(1 - e2 * Cos(ToRadians(d)) * Cos(ToRadians(d)));

            double xi = p.X;
            double eta = p.Y;

            // 8.333-9
            double eta1 = eta / rho1;

            // 8.333-10
            double zeta1_2 = 1 - xi * xi - eta1 * eta1;

            double zeta1;
            if (zeta1_2 >= 0)
            {
                zeta1 = Sqrt(zeta1_2);
            }
            else 
            {
                if (forceProjection)
                {
                    double P = Atan2(eta1, xi);
                    xi = Cos(P);
                    eta1 = Sin(P);
                    zeta1 = 0;
                }
                else
                {
                    return null;
                }
            }

            // 8.334-1
            double sind1 = Sin(ToRadians(d)) / rho1;
            double cosd1 = Sqrt(1 - e2) * Cos(ToRadians(d)) / rho1;

            double d1 = Atan2(sind1, cosd1);

            // 8.333-13
            var v = Matrix.R1(d1) * new Vector(xi, eta1, zeta1);

            double phi1 = Asin(v.Y);
            double sinTheta = v.X / Cos(phi1);
            double cosTheta = v.Z / Cos(phi1);

            double theta = ToDegrees(Atan2(sinTheta, cosTheta));

            double tanPhi = Tan(phi1) / Sqrt(1 - e2);

            double phi = Atan(tanPhi);

            // 8.331-4
            double lambda = mu - theta;

            return new CrdsGeographical(To360(lambda + 180) - 180, ToDegrees(phi));
        }

        /// <summary>
        /// Finds points of intersection of two circles.
        /// First circle is a Unit circle (of radius 1 centered at the origin (0, 0) of fundamental plane).
        /// Second circle is defined by its center (<paramref name="p"/>) and radius (<paramref name="r"/>)
        /// </summary>
        /// <param name="p">Center of the second circle</param>
        /// <param name="r">Radius of the second circle</param>
        /// <returns>
        /// Zero, one or two points of intersection
        /// </returns>
        /// <remarks>
        /// Method is based on algorithms
        /// https://e-maxx.ru/algo/circles_intersection
        /// https://e-maxx.ru/algo/circle_line_intersection
        /// </remarks>
        private static PointF[] CirclesIntersection(PointF p, double r)
        {
            double a = -2 * p.X;
            double b = -2 * p.Y;
            double c = p.X * p.X + p.Y * p.Y + 1 - r * r;

            double x0 = -(a * c) / (a * a + b * b);
            double y0 = -(b * c) / (a * a + b * b);

            // no points of intersection
            if (c * c > a * a + b * b + 1e-7)
            {
                return new PointF[0];
            }
            // one point
            else if (Abs(c * c - (a * a + b * b)) < 1e-7)
            {
                return new PointF[] { new PointF((float)x0, (float)y0) };
            }
            // two points
            else
            {
                double d = Sqrt(1 - (c * c) / (a * a + b * b));
                double mult = Sqrt((d * d) / (a * a + b * b));
                double ax, ay, bx, by;
                ax = x0 + b * mult;
                ay = y0 - a * mult;
                bx = x0 - b * mult;
                by = y0 + a * mult;

                return new[] { 
                    new PointF((float)ax, (float)ay), 
                    new PointF((float)bx, (float)by) }
                .OrderBy(i => -i.Y)
                .ToArray();
            }
        }

        /// <summary>
        /// Finds function root by bisection method
        /// </summary>
        /// <param name="func">Function to find root</param>
        /// <param name="a">Left edge of the interval</param>
        /// <param name="b">Right edge of the interval</param>
        /// <param name="eps">Tolerance</param>
        /// <returns>Function root</returns>
        private static double FindRoots(Func<double, double> func, double a, double b, double eps)
        {
            // check function has different 
            // signs on segment ends
            if (func(b) * func(a) > 0)
                return double.NaN;

            double dx;
            while (b - a > eps)
            {
                dx = (b - a) / 2;
                double c = a + dx;
                if (func(a) * func(c) < 0)
                {
                    b = c;
                }
                else
                {
                    a = c;
                }
            }
            return (a + b) / 2;
        }

        /// <summary>
        /// Calculates nearest solar eclipse (next or previous) for the provided Julian Day.
        /// </summary>
        /// <param name="jd"></param>
        /// <param name="next"></param>
        public static SolarEclipse NearestEclipse(double jd, bool next)
        {
            Date d = new Date(jd);
            double year = d.Year + (Date.JulianEphemerisDay(d) - Date.JulianDay0(d.Year)) / 365.25;
            double k = Floor((year - 2000) * 12.3685);
            bool eclipseFound;
          
            double T = k / 1236.85;
            double T2 = T * T;
            double T3 = T2 * T;
            double T4 = T3 * T;

            SolarEclipse eclipse = new SolarEclipse();

            do
            {
                // Moon's argument of latitude (mean dinstance of the Moon from its ascending node)
                double F = 160.7108 + 390.67050284 * k
                                    - 0.0016118 * T2
                                    - 0.00000227 * T3
                                    + 0.000000011 * T4;
                
                eclipseFound = Abs(Sin(ToRadians(F))) <= 0.36;

                if (eclipseFound)
                {
                    double jdMeanPhase = 2451550.09766 + 29.530588861 * k
                                                    + 0.00015437 * T2
                                                    - 0.000000150 * T3
                                                    + 0.00000000073 * T4;

                    // Sun's mean anomaly
                    double M = 2.5534 + 29.10535670 * k
                                     - 0.0000014 * T2
                                     - 0.00000011 * T3;
                    M = ToRadians(M);

                    // Moon's mean anomaly
                    double M_ = 201.5643 + 385.81693528 * k
                                         + 0.0107582 * T2
                                         + 0.00001238 * T3
                                         - 0.000000058 * T4;
                    M_ = ToRadians(M_);

                    // Mean longitude of ascending node
                    double Omega = 124.7746 - 1.56375588 * k
                                            + 0.0020672 * T2
                                            + 0.00000215 * T3;
                    Omega = ToRadians(Omega);

                    // Multiplier related to the eccentricity of the Earth orbit
                    double E = 1 - 0.002516 * T - 0.0000074 * T2;

                    double F1 = ToRadians(F - 0.02665 * Sin(Omega));
                    double A1 = ToRadians(299.77 + 0.107408 * k - 0.009173 * T2);

                    double jdMax =
                        jdMeanPhase
                        - 0.4075 * Sin(M_)
                        + 0.1721 * E * Sin(M)
                        + 0.0161 * Sin(2 * M_)
                        - 0.0097 * Sin(2 * F1)
                        + 0.0073 * E * Sin(M_ - M)
                        - 0.0050 * E * Sin(M_ + M)
                        - 0.0023 * Sin(M_ - 2 * F1)
                        + 0.0021 * E * Sin(2 * M)
                        + 0.0012 * Sin(M_ + 2 * F1)
                        + 0.0006 * E * Sin(2 * M_ + M)
                        - 0.0004 * Sin(3 * M_)
                        - 0.0003 * E * Sin(M + 2 * F1)
                        + 0.0003 * Sin(A1)
                        - 0.0002 * E * Sin(M - 2 * F1)
                        - 0.0002 * E * Sin(2 * M_ - M)
                        - 0.0002 * Sin(Omega);

                    double P =
                        0.2070 * E * Sin(M)
                        + 0.0024 * E * Sin(2 * M)
                        - 0.0392 * Sin(M_)
                        + 0.0116 * Sin(2 * M_)
                        - 0.0073 * E * Sin(M_ + M)
                        + 0.0067 * E * Sin(M_ - M)
                        + 0.0118 * Sin(2 * F1);

                    double Q = 
                        5.2207 - 0.0048 * E * Cos(M) 
                        + 0.0020 * E * Cos(2 * M) 
                        - 0.3299 * Cos(M_) 
                        - 0.0060 * E * Cos(M_ + M) 
                        + 0.0041 * E * Cos(M_ - M);

                    double W = Abs(Cos(F1));

                    double gamma = (P * Cos(F1) + Q * Sin(F1)) * (1 - 0.0048 * W);

                    double u = 0.0059
                        + 0.0046 * E * Cos(M)
                        - 0.0182 * Cos(M_)
                        + 0.0004 * Cos(2 * M_)
                        - 0.0005 * E * Cos(M + M_);

                    // no eclipse visible from the Earth surface
                    if (Abs(gamma) > 1.5433 + u)
                    {
                        eclipseFound = false;
                        if (next) k++;
                        else k--;
                        continue;
                    }

                    eclipse.U = u;
                    eclipse.Gamma = gamma;
                    eclipse.JulianDayMaximum = jdMax;

                    // non-central eclipse
                    if (Abs(gamma) > 0.9972 && Abs(gamma) < 0.9972 + Abs(u))
                    {
                        eclipse.IsNonCentral = true;
                    }

                    if (u < 0)
                    {
                        eclipse.EclipseType = SolarEclipseType.Total;
                    }
                    else if (u > 0.0047)
                    {
                        eclipse.EclipseType = SolarEclipseType.Annular;
                    }
                    else
                    {
                        double omega = 0.00464 * Sqrt(1 - gamma * gamma);
                        if (u < omega)
                        {
                            eclipse.EclipseType = SolarEclipseType.Hybrid;
                        }
                        else
                        {
                            eclipse.EclipseType = SolarEclipseType.Annular;
                        }
                    }

                    if (!eclipse.IsNonCentral && Abs(gamma) > 0.9972 && Abs(gamma) < 1.5433 + u)
                    {
                        eclipse.EclipseType = SolarEclipseType.Partial;
                        eclipse.Phase = (1.5433 + u - Abs(gamma)) / (0.5461 + 2 * u);
                    }

                    // hemisphere
                    if (gamma > 0)
                    {
                        eclipse.Regio = EclipseRegio.Northern;
                    }
                    if (gamma < 0)
                    {
                        eclipse.Regio = EclipseRegio.Southern;
                    }
                    if (Abs(gamma) < 0.1)
                    {
                        eclipse.Regio = EclipseRegio.Equatorial;
                    }
                }
                else
                {
                    if (next) k++;
                    else k--;
                }
            }
            while (!eclipseFound);

            return eclipse;
        }

        internal static Vector ProjectOnFundamentalPlane(CrdsGeographical g, double d, double mu)
        {
            const double e2 = 0.00669454;

            double phi = ToRadians(g.Latitude);
            double sinPhi = Sin(phi);

            double C = 1.0 / Sqrt(1 - e2 * sinPhi * sinPhi);
            double S = (1 - e2) * C;

            double a = 6378137.0;
            double h = g.Elevation;
            double phi_ = Atan((a * S + h) * Tan(phi) / (a * C + h));
            double rho = (a * C + h) * Cos(phi) / (a * Cos(phi_));

            // 8.331-4
            double theta = mu - g.Longitude; // (b.Mu - 1.002738 * 360.0 / 86400.0 * deltaT) - g.Longitude;

            // 8.331-5
            double xi = rho * Cos(phi_) * Sin(ToRadians(theta));
            double eta = rho * Sin(phi_) * Cos(ToRadians(d)) - rho * Cos(phi_) * Sin(ToRadians(d)) * Cos(ToRadians(theta));
            double zeta = rho * Sin(phi_) * Sin(ToRadians(d)) + rho * Cos(phi_) * Cos(ToRadians(d)) * Cos(ToRadians(theta));

            return new Vector(xi, eta, zeta);
        }

        public static double FindLocalMax(PolynomialBesselianElements pbe, CrdsGeographical g)
        {
            double step = TimeSpan.FromSeconds(1).TotalDays;

            // left edge of time interval
            double jdFrom = pbe.From;

            // right edge of time interval
            double jdTo = pbe.To - step;

            // precision of calculation, in days
            double epsilon = 1e-8;

            Func<double, double> funcLocalMax = (jd) =>
            {
                double[] dist = new double[2];

                for (int i = 0; i < 2; i++)
                {
                    InstantBesselianElements b = pbe.GetInstantBesselianElements(jd + i * step);
                    Vector v = ProjectOnFundamentalPlane(g, b.D, b.Mu);

                    double xi = v.X;
                    double eta = v.Y;

                    dist[i] = Sqrt((xi - b.X) * (xi - b.X) + (eta - b.Y) * (eta - b.Y));
                }
                return (dist[1] - dist[0]) / step;
            };

            return FindRoots(funcLocalMax, jdFrom, jdTo, epsilon);
        }

        public static double Obscuration(PolynomialBesselianElements pbe, CrdsGeographical g, double jdLocalMax)
        {
            InstantBesselianElements b = pbe.GetInstantBesselianElements(jdLocalMax);
            Vector v = ProjectOnFundamentalPlane(g, b.D, b.Mu);

            double xi = v.X;
            double eta = v.Y;
            double zeta = v.Z;

            double delta = Sqrt((xi - b.X) * (xi - b.X) + (eta - b.Y) * (eta - b.Y));

            // Penumra radius at local point, in Earth radii
            double L2 = b.L2 - zeta * Tan(ToRadians(b.F2));

            // Umbra radius at local point, in Earth radii
            double L1 = b.L1 - zeta * Tan(ToRadians(b.F1));

            if (v.Z >= 0)
            {
                // No eclipse visible
                if (delta > L1)
                {
                    return 0;
                }
                // Partial eclipse
                else if (delta <= L1 && delta > Abs(L2))
                {
                    return (L1 - delta) / (L1 + L2);
                }
                else if (delta <= Abs(L2))
                {
                    // Total eclipse
                    if (L2 < 0)
                    {
                        return 1;
                    }
                    // Annular eclipse   
                    else
                    {
                        return (L1 - delta) / (L1 + L2);
                    }
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        public static double LocalCircumstances(PolynomialBesselianElements pbe, CrdsGeographical g)
        {
            double deltaT = pbe.DeltaT ?? Date.DeltaT(pbe.JulianDay0);

            double t = 0;   // time since jd0
            double phi = g.Latitude; // latitude

            double tau;

            int iters = 0;

            // Earth flattening
            const double flat = 1.0 / 298.257;

            // Earth flattening constant, used in calculation: 0.99664719
            double fconst = 1.0 - flat;

            double U = Atan(fconst * Tan(ToRadians(phi)));

            double rhoSinPhi_ = fconst * Sin(U);
            double rhoCosPhi_ = Cos(U);


            double jd;

            double d, H, zeta, u, v, a, b, n2;
            InstantBesselianElements be;

            do
            {
                iters++;

                jd = pbe.JulianDay0 + t * pbe.Step;
                be = pbe.GetInstantBesselianElements(jd);

                double X = be.X;
                double Y = be.Y;
                d = ToRadians(be.D); 
                double M = be.Mu; 

                double dX = be.dX; 
                double dY = be.dY; 

                H = ToRadians(M - g.Longitude - 0.00417807 * deltaT);

                double ksi = rhoCosPhi_ * Sin(H);
                double eta = rhoSinPhi_ * Cos(d) - rhoCosPhi_ * Cos(H) * Sin(d);
                zeta = rhoSinPhi_ * Sin(d) + rhoCosPhi_ * Cos(H) * Cos(d);

                double ksi_ = ToRadians(pbe.Mu[1] * rhoCosPhi_ * Cos(H));
                double eta_ = ToRadians(pbe.Mu[1] * ksi * Sin(d) - zeta * pbe.D[1]);

                u = X - ksi;
                v = Y - eta;

                a = dX - ksi_;
                b = dY - eta_;

                n2 = a * a + b * b;

                tau = -(u * a + v * b) / n2;

                t += tau;
            }
            while (Abs(tau) >= 0.00001 && iters < 20);

            //double sinh = Sin(d) * Sin(ToRadians(phi)) + Cos(d) * Cos(ToRadians(phi)) * Cos(H);

            //double h = ToDegrees(Asin(sinh));

            //if (h < -0.5)
            //{
            // Sun is below horizon
            //return 0;
            //}

            double jdMax = jd;

            double dL1 = be.L1 - zeta * pbe.tanF1;
            double dL2 = be.L2 - zeta * pbe.tanF2;

            double m = Sqrt(u * u + v * v);

            double G = (dL1 - m) / (dL1 + dL2);

            
            
            if (G < 0)
                return 0;

            double n = Sqrt(n2);


            double S = (a * v - u * b) / (n * dL1);

            double tauBegin = -(u * a + v * b) / n2 - dL1 / n * Sqrt(1 - S * S);
            double tauEnd = -(u * a + v * b) / n2 + dL1 / n * Sqrt(1 - S * S);

            return G;
        }
    }
   
    /// <summary>
    /// Describes general details of the Solar eclipse
    /// </summary>
    public class SolarEclipse
    {
        /// <summary>
        /// Instant of maximal eclipse
        /// </summary>
        public double JulianDayMaximum { get; set; }

        /// <summary>
        /// Eclipse phase
        /// </summary>
        public double Phase { get; set; } = 1;
        
        /// <summary>
        /// Regio where the eclipse is primarily visible
        /// </summary>
        public EclipseRegio Regio { get; set; }        
               
        /// <summary>
        /// Least distance from the axis of the Moon's shadow to the center of the Earth,
        /// in units of equatorial radius of the Earth.
        /// </summary>
        public double Gamma { get; set; }

        /// <summary>
        /// Radius of the Moon's umbral cone in the fundamental plane,
        /// in units of equatorial radius of the Earth.
        /// </summary>
        public double U { get; set; }

        /// <summary>
        /// Type of eclipse: annular, central, hybrid (annular-central) or partial
        /// </summary>
        public SolarEclipseType EclipseType { get; set; }

        /// <summary>
        /// Flag indicating the eclipse is non-central
        /// (umbral cone touches the Earth polar regio but umbral axis does not)
        /// </summary>
        public bool IsNonCentral { get; set; }
    } 

    /// <summary>
    /// Solar eclipse type
    /// </summary>
    public enum SolarEclipseType
    {
        /// <summary>
        /// Annular solar eclipse
        /// </summary>
        Annular,

        /// <summary>
        /// Total solar eclipse
        /// </summary>
        Total,

        /// <summary>
        /// Hybrid, or annular-total solar eclipse
        /// </summary>
        Hybrid,

        /// <summary>
        /// Partial solar eclipse
        /// </summary>
        Partial
    }

    /// <summary>
    /// Visibility regio of the eclipse
    /// </summary>
    public enum EclipseRegio
    {
        /// <summary>
        /// Eclipse is primarily visible in Northern hemisphere
        /// </summary>
        Northern = 1,

        /// <summary>
        /// Eclipse is primarily visible in equatorial regio
        /// </summary>
        Equatorial = 0,

        /// <summary>
        /// Eclipse is primarily visible in Southern hemisphere
        /// </summary>
        Southern = -1
    }
}
