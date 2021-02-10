﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astrarium.Algorithms
{
    /// <summary>
    /// Describes general details of the lunar eclipse
    /// </summary>
    public class LunarEclipse
    {
        /// <summary>
        /// Instant of maximal eclipse
        /// </summary>
        public double JulianDayMaximum { get; set; }

        /// <summary>
        /// Eclipse phase
        /// </summary>
        public double Magnitude { get; set; }

        /// <summary>
        /// Radius of penumbra, in equatorial Earth radii, at eclipse plane
        /// </summary>
        public double Rho { get; set; }

        /// <summary>
        /// Radius of umbra, in equatorial Earth radii, at eclipse plane
        /// </summary>
        public double Sigma { get; set; }

        /// <summary>
        /// Least distance from the center of the Moon to the axis of the Earth shadow,
        /// in units of equatorial radius of the Earth.
        /// </summary>
        public double Gamma { get; set; }

        /// <summary>
        /// Radius of the Earth umbral cone in the eclipse plane,
        /// in units of equatorial radius of the Earth.
        /// </summary>
        public double U { get; set; }

        /// <summary>
        /// Type of eclipse: total, partial, penumbral  
        /// </summary>
        public LunarEclipseType EclipseType { get; set; }

        /// <summary>
        /// Instant of first contact with penumbra (P1)
        /// </summary>
        public double JulianDayFirstContactPenumbra { get; set; }

        /// <summary>
        /// Instant of first contact with umbra (U1)
        /// </summary>
        public double JulianDayFirstContactUmbra { get; set; }

        /// <summary>
        /// Instant of beginning of total phase (U2)
        /// </summary>
        public double JulianDayTotalBegin { get; set; }

        /// <summary>
        /// Instant of end of total phase (U3)
        /// </summary>
        public double JulianDayTotalEnd { get; set; }

        /// <summary>
        /// Instant of last contact with umbra (U4)
        /// </summary>
        public double JulianDayLastContactUmbra { get; set; }

        /// <summary>
        /// Instant of last contact with penumbra (P4)
        /// </summary>
        public double JulianDayLastContactPenumbra { get; set; }

        /// <summary>
        /// Duration of penumbral phase, in days
        /// </summary>
        public double PenumbralDuration => JulianDayLastContactPenumbra - JulianDayFirstContactPenumbra;

        /// <summary>
        /// Duration of umbral phase (partial phases), in days
        /// </summary>
        public double PartialDuration => JulianDayLastContactUmbra - JulianDayFirstContactUmbra;

        /// <summary>
        /// Duration of total phase, in days
        /// </summary>
        public double TotalityDuration => JulianDayTotalEnd - JulianDayTotalBegin;

        #region Helpers

        private string JdToString(double jd)
        {
            if (double.IsNaN(jd) || jd == 0)
                return "---";
            else
            {
                var date = new Date(jd, 0);
                var culture = CultureInfo.InvariantCulture;
                string month = culture.DateTimeFormat.GetMonthName(date.Month);
                int day = (int)date.Day;
                int year = date.Year;
                return string.Format($"{day} {month} {year} {TimeToString(date.Day - day)} UT");
            }
        }

        private string TimeToString(double time)
        {
            if (double.IsNaN(time) || time == 0)
                return "-";
            else
            {
                var ts = TimeSpan.FromDays(time);
                return string.Format($"{ts:hh\\:mm\\:ss}");
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return
                new StringBuilder()
                    .AppendLine($"First contant with penumbra = {JdToString(JulianDayFirstContactPenumbra)}")
                    .AppendLine($"First contact with umbra = {JdToString(JulianDayFirstContactUmbra)}")
                    .AppendLine($"Beginning of total phase = {JdToString(JulianDayTotalBegin)}")
                    .AppendLine($"Maximum of the eclipse = {JdToString(JulianDayMaximum)}")
                    .AppendLine($"End of total phase = {JdToString(JulianDayTotalEnd)}")
                    .AppendLine($"Last contact with umbra = {JdToString(JulianDayLastContactUmbra)}")
                    .AppendLine($"Last contact with penumbra = {JdToString(JulianDayLastContactPenumbra)}")
                    .AppendLine($"Totality duration = {TimeToString(TotalityDuration)}")
                    .AppendLine($"Partial duration = {TimeToString(PartialDuration)}")
                    .AppendLine($"Penumbral duration = {TimeToString(PenumbralDuration)}")
                    .ToString();
        }

        #endregion Helpers
    }

    public class LunarEclipseLocalCircumstancesContactPoint
    {
        public double JulianDay { get; private set; }
        public double LunarAltitude { get; private set; }
        
        public LunarEclipseLocalCircumstancesContactPoint(double jd, double alt)
        {
            JulianDay = jd;
            LunarAltitude = alt;
        }
    }

    public class LunarEclipseLocalCircumstances
    {
        /// <summary>
        /// Geographical location
        /// </summary>
        public CrdsGeographical Location { get; set; }

        // TODO: docs
        public LunarEclipseLocalCircumstancesContactPoint PenumbralBegin { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint PartialBegin { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint TotalBegin { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint Maximum { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint TotalEnd { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint PartialEnd { get; set; }
        public LunarEclipseLocalCircumstancesContactPoint PenumbralEnd { get; set; }
    }

    public class LunarEclipseContact
    {
        /// <summary>
        /// Contact instant
        /// </summary>
        public double JuluanDay { get; set; }
        
        /// <summary>
        /// Equatorial geocentrical coordinates of the Moon
        /// </summary>
        public CrdsEquatorial MoonCoordinates { get; set; }

        /// <summary>
        /// Apparent sidereal time at Greenwich, in degrees
        /// </summary>
        public double SiderealTime { get; set; }

        /// <summary>
        /// Horizontal equatorial parallax of the Moon
        /// </summary>
        public double Parallax { get; set; }
    }

    public class LunarEclipseContacts
    {
        /// <summary>
        /// Instant and geocentrical coordinates of first contact with penumbra (P1)
        /// </summary>
        public LunarEclipseContact PenumbralBegin { get; set; }

        /// <summary>
        /// Instant and geocentrical coordinates of first contact with umbra (U1)
        /// </summary>
        public LunarEclipseContact PartialBegin { get; set; }

        /// <summary>
        /// Instant and geocentrical coordinates of beginning of total phase (U2)
        /// </summary>
        public LunarEclipseContact TotalBegin { get; set; }

        /// <summary>
        /// Instant and geocentrical coordinates of maximal eclipse (Max)
        /// </summary>
        public LunarEclipseContact Maximum { get; set; }

        /// <summary>
        /// Instant and geocentrical coordinates of end of total phase (U3)
        /// </summary>
        public LunarEclipseContact TotalEnd { get; set; }

        /// <summary>
        /// Instant and coordinates of last contact with umbra (U4)
        /// </summary>
        public LunarEclipseContact PartialEnd { get; set; }

        /// <summary>
        /// Instant and geocentrical coordinates of last contact with penumbra (P4)
        /// </summary>
        public LunarEclipseContact PenumbralEnd { get; set; }
    }
}
