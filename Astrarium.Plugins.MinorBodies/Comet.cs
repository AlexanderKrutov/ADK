﻿using Astrarium.Algorithms;
using Astrarium.Types;

namespace Astrarium.Plugins.MinorBodies
{
    public class Comet : SizeableCelestialObject, IMovingObject
    {
        /// <summary>
        /// Name or readable designation of the comet
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Orbital elements of the comet
        /// </summary>
        public OrbitalElements Orbit { get; set; }

        /// <summary>
        /// Absolute magnitude
        /// </summary>
        public double H { get; set; }

        /// <summary>
        /// Slope parameter
        /// </summary>
        public double G { get; set; }

        /// <summary>
        /// Magnitude of comet
        /// </summary>
        public float Magnitude { get; set; }

        // Average daily motion of comet
        public double AverageDailyMotion { get; set; }

        /// <summary>
        /// Visible horizontal coordinates of comet tail end
        /// </summary>
        public CrdsHorizontal TailHorizontal { get; set; }

        /// <summary>
        /// Gets comet names
        /// </summary>
        public override string[] Names => new[] { Name };

        /// <summary>
        /// Name of the setting(s) responsible for displaying the object
        /// </summary>
        public override string[] DisplaySettingNames => new[] { "Comets" };
    }
}
