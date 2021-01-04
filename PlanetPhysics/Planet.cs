using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlanetPhysics
{
	public class Planet
	{
		public Planet(Color colour, float radius, Vector2 velocity, Vector2 displacement, float mass, bool isDebris = false)
		{
			Colour = colour;
			Radius = radius;
			Velocity = velocity;
			Displacement = displacement;
			Mass = mass;
			IsDebris = isDebris;
		}

		public Color Colour { get; set; }
		public float Radius { get; set; }
		public Vector2 Velocity { get; set; }
		public Vector2 Displacement { get; set; }
		public float Mass { get; set; }
		public Vector2 Forces { get; set; }
		public bool IsDebris { get; set; }
		public float TimeExisted { get; set; }
	}
}
