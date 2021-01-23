using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PlanetPhysics
{
	public class Game1 : Game
	{
		private GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;

		private SpriteFont debugFont;
		private string debugText = "";

		private Vector2 windowSize = new Vector2(1920, 1080);

		// All planets and debris
		private List<Planet> planets = new List<Planet>();

		// Any planets to be added
		private List<Planet> toAdd = new List<Planet>();
		// Any planets to be removed
		private List<Planet> toRemove = new List<Planet>();

		private Vector2 cameraPos;
		private float scaleMod = 80;
		private int prevScroll = 0;

		private Vector2 sysBaseVel = new Vector2(10, -10);

		private Vector2 mouseStartPos;
		private Vector2 mouseCurrentPos;
		private bool preparingToAdd = false;

		private float desiredRadius = 0.1f;
		private float desiredMass = 10;

		private Planet focusPlanet;

		Random random = new Random();

		private Thread physicsThread;

		public Game1()
		{
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			cameraPos = windowSize / 2;
		}

		protected override void Initialize()
		{
			// Change the window size, as the default size is too small
			_graphics.PreferredBackBufferWidth = (int)windowSize.X;
			_graphics.PreferredBackBufferHeight = (int)windowSize.Y;
			_graphics.ApplyChanges();

			bool shouldGenSystem = true;

			if (shouldGenSystem)
			{
				// Initialise the system with a bunch of planets
				// Sun
				planets.Add(new Planet(Color.Yellow, 0.2f, sysBaseVel, Vector2.Zero, 1000));
				focusPlanet = planets[0];

				planets.Add(new Planet(Color.Red, 0.05f, new Vector2(0, 30) + sysBaseVel, new Vector2(1, 0), 1));

				planets.Add(new Planet(Color.Blue, 0.075f, new Vector2(-17.5f, 0) + sysBaseVel, new Vector2(0, 3), 2));

				planets.Add(new Planet(Color.Green, 0.1f, new Vector2(16, 0) + sysBaseVel, new Vector2(0, -4), 10));

				planets.Add(new Planet(Color.White, 0.05f, new Vector2(-2, -2) + sysBaseVel, new Vector2(-6, 6), 0.5f));

				GenAsteroidBelt(6, 7, 1000);
				GenAsteroidBelt(1.5f, 2.5f, 300);
			}

			base.Initialize();

			physicsThread = new Thread(PhysicsUpdate) { IsBackground = true };
			physicsThread.Start();
		}

		private void GenAsteroidBelt(float rMin, float rMax, int astCount)
		{
			for (int i = 0; i < astCount; i++)
			{
				float rOrbit = rMin + (float)random.NextDouble() * (rMax - rMin);
				float pos = (float)random.NextDouble() * 2 * MathF.PI;
				float vel = MathF.Sqrt(planets[0].Mass / rOrbit);
				planets.Add(new Planet(Color.Gray, 1, vel * new Vector2(-MathF.Sin(pos), MathF.Cos(pos)) + sysBaseVel, rOrbit * new Vector2(MathF.Cos(pos), MathF.Sin(pos)), 1, true));
			}
		}

		protected override void LoadContent()
		{
			_spriteBatch = new SpriteBatch(GraphicsDevice);

			debugFont = Content.Load<SpriteFont>("DebugFont");
		}

		protected override void Update(GameTime gameTime)
		{
			KeyboardState kState = Keyboard.GetState();

			// Allow the user to exit by pressing the escape key
			if (kState.IsKeyDown(Keys.Escape))
				Exit();

			// Allow the camera position to be changed
			float moveSpeed = 4;
			if (kState.IsKeyDown(Keys.LeftShift)) moveSpeed *= 3;

			if (kState.IsKeyDown(Keys.A)) cameraPos.X += moveSpeed;
			if (kState.IsKeyDown(Keys.D)) cameraPos.X -= moveSpeed;
			if (kState.IsKeyDown(Keys.W)) cameraPos.Y += moveSpeed;
			if (kState.IsKeyDown(Keys.S)) cameraPos.Y -= moveSpeed;

			MouseState mState = Mouse.GetState();

			bool zoomedIn = mState.ScrollWheelValue > prevScroll;
			bool zoomedOut = mState.ScrollWheelValue < prevScroll;

			if (kState.IsKeyDown(Keys.LeftControl))
			{
				if (zoomedIn) desiredRadius *= 1.1f;
				if (zoomedOut) desiredRadius /= 1.1f;
			}
			if (kState.IsKeyDown(Keys.LeftShift))
			{
				if (zoomedIn) desiredMass *= 1.1f;
				if (zoomedOut) desiredMass /= 1.1f;
			}
			if (kState.IsKeyUp(Keys.LeftControl) && kState.IsKeyUp(Keys.LeftShift))
			{
				// TODO: Should zoom out from centre of screen
				//if (zoomedIn || zoomedOut)
				//{
				//	cameraPos -= windowSize / 2;
				//	cameraPos /= scaleMod;
				//}

				// Allow the user to zoom in and out
				if (zoomedIn)
				{
					scaleMod *= 1.1f;
				}
				else if (zoomedOut)
				{
					scaleMod /= 1.1f;
				}
			}
			prevScroll = mState.ScrollWheelValue;

			// Allow the user to add planets with the desired position/velocity
			mouseCurrentPos = mState.Position.ToVector2();

			// If the user has just clicked
			if (!preparingToAdd && mState.LeftButton == ButtonState.Pressed)
			{
				mouseStartPos = mouseCurrentPos;
				preparingToAdd = true;
			}

			// If the user has just released their click
			if (preparingToAdd && mState.LeftButton == ButtonState.Released)
			{
				preparingToAdd = false;

				Vector2 mouseDiff = mouseCurrentPos - mouseStartPos;
				float length = mouseDiff.LengthSquared();
				
				// Velocity is proportional to the distance moved by the mouse squared
				Vector2 velocity;
				length /= 100;
				if (length > 0) velocity = mouseDiff.NormalizedCopy() * length;
				else velocity = Vector2.Zero;

				if (!(focusPlanet is null))
				{
					velocity += focusPlanet.Velocity;
				}
				
				Vector2 position = (mouseStartPos - cameraPos) / scaleMod;
				
				planets.Add(new Planet(Color.SkyBlue, desiredRadius, velocity, position, desiredMass));
				
				debugText = position.ToString();
			}

			// Create debris if the right mouse button is held down
			if (mState.RightButton == ButtonState.Pressed)
			{
				Vector2 position = (mState.Position.ToVector2() - cameraPos) / scaleMod;
				for (int i = 0; i < 10; i++)
				{
					random.NextUnitVector(out Vector2 velocity);
					Planet particle = new Planet(Color.SkyBlue, 1, velocity * random.Next(600, 700), position, 1000, true);
					planets.Add(particle);
				}
			}

			base.Update(gameTime);
		}

		private void PhysicsUpdate()
		{
			Stopwatch watch = new Stopwatch();
			watch.Start();
			while (true)
			{
				float tPassed = watch.ElapsedMilliseconds / 1000f / 10f;
				watch.Restart();
				// Update the time existed and check if debris should be removed
				Planet[] pArr = planets.ToArray();

				foreach (Planet p in pArr)
				{
					p.TimeExisted += tPassed;

					// Remove debris after it has existed for roughly 15 seconds to help improve performance
					if (p.IsDebris && p.TimeExisted > 15)
					{
						toRemove.Add(p);
						continue;
					}
				}

				// Update the forces on all planets
				foreach (Planet p in pArr)
				{
					// If a planet is about to be removed, there is no point in considering where it is going to go
					if (toRemove.Contains(p)) continue;

					Vector2 forces = p.Forces;

					foreach (Planet pOther in pArr)
					{
						// A planet cannot apply forces to itself
						if (pOther == p) continue;
						// Ignore forces from debris
						if (pOther.IsDebris) continue;

						float distSqr = (p.Displacement - pOther.Displacement).LengthSquared();

						Vector2 direction = (p.Displacement - pOther.Displacement).NormalizedCopy();

						// Calculate gravity.
						float f = p.Mass * pOther.Mass / distSqr;

						forces -= direction * f;
					}

					p.Forces = forces;
				}

				// Deal with collisions
				// TODO: Currently disabled
				foreach (Planet p in pArr)
				{
					continue;
					foreach (Planet pColliding in pArr)
					{
						// A planet cannot collide with itself
						if (pColliding == p) continue;
						// To increase performance, debris only needs to be checked once, as it is immediately removed on collisions
						if (pColliding.IsDebris) continue;
						// Ignore collisions if they are both debris
						if (p.IsDebris && pColliding.IsDebris) continue;

						// If the two planets are colliding
						float dist = (p.Displacement - pColliding.Displacement).Length();
						dist = MathF.Max(dist, (p.Radius + pColliding.Radius));
						if (dist < pColliding.Radius + p.Radius)
						{
							float distMult = 1 / ((dist - p.Radius) / pColliding.Radius);
							p.Forces += (p.Displacement - pColliding.Displacement).NormalizedCopy() * distMult * distMult * pColliding.Mass / p.Mass * 10000000;
						}
					}


					continue;
					if (toRemove.Contains(p)) continue;

					foreach (Planet pColliding in pArr)
					{
						// A planet cannot collide with itself
						if (pColliding == p) continue;
						// To increase performance, debris only needs to be checked once, as it is immediately removed on collisions
						if (pColliding.IsDebris) continue;
						// Ignore collisions if they are both debris
						if (p.IsDebris && pColliding.IsDebris) continue;

						// If the two planets are colliding
						float dist = (p.Displacement - pColliding.Displacement).Length();
						if (dist < pColliding.Radius + p.Radius)
						{
							// Remove debris when it collides with other planets without updating mass, velocity etc. of the planet it collides with
							if (p.IsDebris) toRemove.Add(p);
							if (pColliding.IsDebris) toRemove.Add(pColliding);
							if (p.IsDebris || pColliding.IsDebris) continue;

							// Both planets are removed and replaced with a new one
							toRemove.Add(p);
							toRemove.Add(pColliding);

							// The colour of the new planet should be somewhere between the old two colours, based on their masses
							Color newC = Color.Lerp(pColliding.Colour, p.Colour, p.Mass / pColliding.Mass);

							// Work out the areas of both planets
							// Note: No need to multiply by pi here, as to work out the new radius you'd need to divide by pi again
							float a1 = p.Radius * p.Radius;
							float a2 = pColliding.Radius * pColliding.Radius;
							// Work out the new radius
							float newR = MathF.Sqrt(a1 + a2);

							// Work out new velocity based on conservation of momentum
							Vector2 newV = (p.Velocity * p.Mass + pColliding.Velocity * pColliding.Mass) / (pColliding.Mass + p.Mass);

							// The new displacement should be somewhere between the old two
							// Work out where based on masses of planets
							Vector2 newD = (p.Displacement * p.Mass + pColliding.Displacement * pColliding.Mass) / (pColliding.Mass + p.Mass);

							// Create the new planet
							Planet newP = new Planet(newC, newR, newV, newD, p.Mass + pColliding.Mass);
							toAdd.Add(newP);

							// Add some debris 
							AddDebris(newP, newC);
						}
					}
				}

				foreach (Planet p in pArr)
				{
					if (toRemove.Contains(p)) continue;

					// Update velocity
					p.Velocity += p.Forces / p.Mass * tPassed;
					// Update displacement
					p.Displacement += p.Velocity * tPassed;
					// Reset forces
					p.Forces = Vector2.Zero;

					if (Math.Round(p.TimeExisted, 3, MidpointRounding.ToZero) > Math.Round(p.PrevTimeUpdated, 3, MidpointRounding.ToZero))
					{
						p.PointIndex++;
						if (p.PointIndex == p.PrevPoints.Length)
						{
							p.PointIndex = 0;
							p.HasRepeated = true;
						}
					}

					p.PrevPoints[p.PointIndex] = p.Displacement;

					p.PrevTimeUpdated = p.TimeExisted;
				}

				if (!(focusPlanet is null))
				{
					cameraPos = -focusPlanet.Displacement * scaleMod + windowSize / 2;
				}

				foreach (Planet p in toRemove) planets.Remove(p);
				planets.AddRange(toAdd);

				toRemove.Clear();
				toAdd.Clear();

				while (watch.Elapsed.TotalMilliseconds <= 1) { }
			}
		}

		private void AddDebris(Planet planet, Color colour)
		{
			// Prevent too much debris from being created, as that has a significant performance impact
			float count;
			if (planets.Count < 100) count = 4;
			else if (planets.Count < 200) count = 3;
			else if (planets.Count < 300) count = 2;
			else if (planets.Count < 400) count = 1;
			else count = 0.5f;

			// Add some debris at random points just above the planet's surface
			for (int i = 0; i < planet.Radius * count; i++)
			{
				random.NextUnitVector(out Vector2 dir);
				Planet particle = new Planet(colour, 1, dir * random.Next(600, 700), dir * planet.Radius * 1.1f + planet.Displacement, 10, true);
				// Vary initial time existed so all debris from same collision doesn't disappear at the same time
				particle.TimeExisted = (float)((random.NextDouble() - 0.5) * 2);
				toAdd.Add(particle);
			}
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(new Color(20, 20, 20));

			_spriteBatch.Begin();
			// Draw all planets
			Planet[] ps = planets.ToArray();

			// Draw trails
			foreach (Planet p in ps)
			{
				if (p.IsDebris) continue;

				for (int i = 0; i < p.PrevPoints.Length - 1; i++)
				{
					if (i == p.PointIndex) continue;
					DrawTrailPoint(p.PrevPoints[i], p.PrevPoints[i + 1], p.Colour);
				}
				
				if (p.PointIndex != p.PrevPoints.Length - 1 && p.HasRepeated) DrawTrailPoint(p.PrevPoints[0], p.PrevPoints[^1], p.Colour);
			}

			foreach (Planet p in ps)
			{
				if (p.IsDebris) _spriteBatch.DrawPoint(p.Displacement * scaleMod + cameraPos, p.Colour, MathF.Max(0.05f * scaleMod, 1));
				else _spriteBatch.DrawCircle(p.Displacement * scaleMod + cameraPos, p.Radius * scaleMod, 20, p.Colour, 0.05f * scaleMod);
			}
			// Show the user where they're aiming when preparing to add a new planet
			if (preparingToAdd) _spriteBatch.DrawLine(mouseStartPos, mouseCurrentPos, Color.Red);

			//_spriteBatch.DrawString(debugFont, debugText, Vector2.Zero, new Color(200, 200, 200));
			_spriteBatch.DrawString(debugFont, $"Desired Mass: {desiredMass}\nDesired radius: {desiredRadius}", Vector2.Zero, new Color(200, 200, 200));

			_spriteBatch.End();

			base.Draw(gameTime);
		}

		private void DrawTrailPoint(Vector2 p1, Vector2 p2, Color colour)
		{
			_spriteBatch.DrawLine(p1 * scaleMod + cameraPos, p2 * scaleMod + cameraPos, colour, 3);
		}
	}
}
