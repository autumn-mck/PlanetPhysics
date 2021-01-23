using Microsoft.Xna.Framework;

namespace PlanetPhysics
{
	public class Planet
	{
		public Planet(Color colour, float radius, Vector2 velocity, Vector2 displacement, float mass, bool isDebris = false, bool isAsteroid = false)
		{
			Colour = colour;
			Radius = radius;
			Velocity = velocity;
			Displacement = displacement;
			Mass = mass;
			IsDebris = isDebris;
			IsAsteroid = isAsteroid;

			PrevTimeUpdated = 0;
			if (!IsDebris && !IsAsteroid)
			{
				PrevPoints = new Vector2[1000];
				PrevPoints[0] = displacement;
				PointIndex = 1;
				HasRepeated = false;
			}
		}

		public Color Colour { get; set; }
		public float Radius { get; set; }
		public Vector2 Velocity { get; set; }
		public Vector2 Displacement { get; set; }
		public float Mass { get; set; }
		public Vector2 Forces { get; set; }
		public bool IsDebris { get; set; }
		public bool IsAsteroid { get; set; }
		public float TimeExisted { get; set; }

		public bool HasRepeated { get; set; }
		public int PointIndex { get; set; }
		public float PrevTimeUpdated { get; set; }
		public Vector2[] PrevPoints { get; set; }
	}
}
