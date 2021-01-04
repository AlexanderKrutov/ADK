﻿using System;
using static System.Math;
using static Astrarium.Algorithms.Angle;

namespace Astrarium.Algorithms
{
    /// <summary>
    /// Contains methods for calculating lunar eclpses
    /// </summary>
    public static class LunarEclipses
    {
        /// <summary>
        /// Calculates nearest lunar eclipse (next or previous) for the provided Julian Day.
        /// </summary>
        /// <param name="jd">Julian day of interest, the nearest lunar eclipse for that date will be found.</param>
        /// <param name="next">Flag indicating searching direction. True means searching next eclipse, false means previous.</param>
        public static LunarEclipse NearestEclipse(double jd, bool next)
        {
            Date d = new Date(jd);
            double year = d.Year + (Date.JulianEphemerisDay(d) - Date.JulianDay0(d.Year)) / 365.25;
            double k = Floor((year - 2000) * 12.3685) + 0.5;
            bool eclipseFound;

            double T = k / 1236.85;
            double T2 = T * T;
            double T3 = T2 * T;
            double T4 = T3 * T;

            LunarEclipse eclipse = new LunarEclipse();

            do
            {
                // Moon's argument of latitude (mean dinstance of the Moon from its ascending node)
                double F = 160.7108 + 390.67050284 * k
                                    - 0.0016118 * T2
                                    - 0.00000227 * T3
                                    + 0.000000011 * T4;

                F = To360(F);
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
                    M = To360(M);
                    M = ToRadians(M);

                    // Moon's mean anomaly
                    double M_ = 201.5643 + 385.81693528 * k
                                         + 0.0107582 * T2
                                         + 0.00001238 * T3
                                         - 0.000000058 * T4;
                    M_ = To360(M_);
                    M_ = ToRadians(M_);

                    // Mean longitude of ascending node
                    double Omega = 124.7746 - 1.56375588 * k
                                            + 0.0020672 * T2
                                            + 0.00000215 * T3;

                    Omega = To360(Omega);
                    Omega = ToRadians(Omega);

                    // Multiplier related to the eccentricity of the Earth orbit
                    double E = 1 - 0.002516 * T - 0.0000074 * T2;

                    double F1 = ToRadians(F - 0.02665 * Sin(Omega));
                    double A1 = ToRadians(To360(299.77 + 0.107408 * k - 0.009173 * T2));

                    double jdMax =
                        jdMeanPhase
                        - 0.4065 * Sin(M_)
                        + 0.1727 * E * Sin(M)
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

                    double rho = 1.2848 + u;
                    double sigma = 0.7403 - u;

                    double mag = (1.0128 - u - Abs(gamma)) / 0.5450;

                    if (mag >= 1)
                    {
                        eclipse.EclipseType = LunarEclipseType.Total;
                    }
                    if (mag > 0 && mag < 1)
                    {
                        eclipse.EclipseType = LunarEclipseType.Partial;
                    }

                    // Check if elipse is penumbral only
                    if (mag < 0)
                    {
                        eclipse.EclipseType = LunarEclipseType.Penumbral;
                        mag = (1.5573 + u - Abs(gamma)) / 0.5450;
                    }

                    // No eclipse, if both phases is less than 0.
                    // Examine other lunation 
                    if (mag < 0)
                    {
                        eclipseFound = false;
                    }
                    // Eclipse found
                    else 
                    {
                        eclipse.JulianDayMaximum = jdMax;
                        eclipse.Magnitude = mag;
                        eclipse.Rho = rho;
                        eclipse.Sigma = sigma;

                        double p = 1.0128 - u;
                        double t = 0.4678 - u;
                        double n = 1.0 / (24 * (0.5458 + 0.04 * Cos(M_)));
                        double h = 1.5573 + u;

                        double sdPartial = n * Sqrt(p * p - gamma * gamma);
                        double sdTotal = n * Sqrt(t * t - gamma * gamma);
                        double sdPenumbra = n * Sqrt(h * h - gamma * gamma);

                        eclipse.JulianDayFirstContactPenumbra = jdMax - sdPenumbra;
                        eclipse.JulianDayFirstContactUmbra = jdMax - sdPartial;
                        eclipse.JulianDayTotalBegin = jdMax - sdTotal;
                        eclipse.JulianDayTotalEnd = jdMax + sdTotal;
                        eclipse.JulianDayLastContactUmbra = jdMax + sdPartial;
                        eclipse.JulianDayLastContactPenumbra = jdMax + sdPenumbra;
                    }
                }
                
                if (!eclipseFound)
                {
                    if (next) k++;
                    else k--;
                }
            }
            while (!eclipseFound);

            return eclipse;
        }
    }
}