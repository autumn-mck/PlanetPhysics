using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

		private Vector2 sysBaseVel = new Vector2(0, 0);

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
			cameraPos = Vector2.Zero;
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
				//sysBaseVel = new Vector2(10, -10);

				// Initialise the system with a bunch of planets
				// Sun
				Planet sun = new Planet(Color.Yellow, 0.2f, sysBaseVel, Vector2.Zero, 1000);
				planets.Add(sun);

				GenPlanetWOrbit(sun, new Vector2(1, 0), Color.Red, 0.05f, 05f, 1);

				GenPlanetWOrbit(sun, new Vector2(0, 3.5f), Color.Blue, 0.075f, 1, 1);
				GenPlanetWOrbit(sun, new Vector2(0, -4.5f), Color.Purple, 0.075f, 1, 1);

				GenPlanetWOrbit(sun, new Vector2(0, -10), Color.Green, 0.1f, 5, 1);
				GenPlanetWOrbit(planets[^1], new Vector2(0, -0.3f), new Color(192, 164, 0), 0.01f, 0.01f, 1.5f);

				GenPlanetWOrbit(sun, new Vector2(-15, 0), Color.Gold, 0.17f, 5, -1);
				GenPlanetWOrbit(planets[^1], new Vector2(-0.5f, 0), Color.SkyBlue, 0.01f, 0.01f, -1.7f);

				// Comet
				planets.Add(new Planet(Color.White, 0.05f, new Vector2(-2, -2) + sysBaseVel, new Vector2(-6, 6), 0.5f));

				GenAsteroidBelt(6, 7, 500);
				GenAsteroidBelt(1.5f, 2.5f, 150);
				GenAsteroidBelt(19, 22, 1000);

				//focusPlanet = planets[0];
			}

			base.Initialize();

			physicsThread = new Thread(PhysicsUpdate) { IsBackground = true };
			physicsThread.Start();
		}

		private void GenPlanetWOrbit(Planet toOrbit, Vector2 diff, Color colour, float radius, float mass, float dir)
		{
			float rOrbit = diff.Length();
			float angle = MathF.Atan(diff.Y / diff.X);
			float vel = MathF.Sqrt(toOrbit.Mass / rOrbit) * dir;
			planets.Add(new Planet(colour, radius, vel * new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) + sysBaseVel, toOrbit.Displacement + diff, mass));
		}

		private void GenAsteroidBelt(float rMin, float rMax, int astCount)
		{
			for (int i = 0; i < astCount; i++)
			{
				float rOrbit = rMin + (float)random.NextDouble() * (rMax - rMin);
				float pos = (float)random.NextDouble() * 2 * MathF.PI;
				float vel = MathF.Sqrt(planets[0].Mass / rOrbit);
				planets.Add(new Planet(Color.Gray, 0.01f, vel * new Vector2(-MathF.Sin(pos), MathF.Cos(pos)) + sysBaseVel, rOrbit * new Vector2(MathF.Cos(pos), MathF.Sin(pos)), 1, false, true));
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
				// Allow the user to zoom in and out
				if (zoomedIn)
				{
					scaleMod *= 1.1f;
					cameraPos *= 1.1f;
				}
				else if (zoomedOut)
				{
					scaleMod /= 1.1f;
					cameraPos /= 1.1f;
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

				Vector2 position = GetMousePosInSpace(mouseStartPos);
				
				planets.Add(new Planet(Color.SkyBlue, desiredRadius, velocity, position, desiredMass));
				
				debugText = position.ToString();
			}

			if (mState.MiddleButton == ButtonState.Pressed)
			{
				Vector2 mPosInSpace = GetMousePosInSpace(mouseCurrentPos);
				Planet nearest = GetNearestPlanetToPoint(mPosInSpace);
				// If the user middle clicks near enough to the planet, it should still count
				float leeway = 4;
				if ((nearest.Displacement - mPosInSpace).LengthSquared() < nearest.Radius * nearest.Radius * leeway)
				{
					focusPlanet = nearest;
				}
			}

			// Create debris if the right mouse button is held down
			if (mState.RightButton == ButtonState.Pressed)
			{
				Vector2 position = GetMousePosInSpace(mouseCurrentPos);
				for (int i = 0; i < 10; i++)
				{
					random.NextUnitVector(out Vector2 velocity);
					Planet particle = new Planet(Color.SkyBlue, 0.01f, velocity * random.Next(6, 7) + planets[0].Velocity, position, 1000, true);
					planets.Add(particle);
				}
			}

			base.Update(gameTime);
		}

		private Vector2 GetMousePosInSpace(Vector2 mousePos)
		{
			return (mousePos - cameraPos - windowSize / 2) / scaleMod;
		}

		private Planet GetNearestPlanetToPoint(Vector2 point)
		{
			Planet[] pLocal = planets.ToArray();
			return pLocal.Aggregate((curMin, p) => curMin == null || (p.Displacement - point).LengthSquared() < (curMin.Displacement - point).LengthSquared() ? p : curMin);
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
						if (pOther.IsDebris || pOther.IsAsteroid) continue;

						float distSqr = (p.Displacement - pOther.Displacement).LengthSquared();

						Vector2 direction = (p.Displacement - pOther.Displacement).NormalizedCopy();

						// Calculate gravity.
						float f = p.Mass * pOther.Mass / distSqr;

						forces -= direction * f;
					}

					p.Forces = forces;
				}

				// Deal with collisions
				foreach (Planet p in pArr)
				{
					foreach (Planet pColliding in pArr)
					{
						// A planet cannot collide with itself
						if (pColliding == p) continue;
						// To increase performance, debris only needs to be checked once, as it is immediately removed on collisions
						if (pColliding.IsDebris || pColliding.IsAsteroid) continue;
						// Ignore collisions if they are both debris
						if ((p.IsDebris || p.IsAsteroid) && (pColliding.IsDebris || pColliding.IsAsteroid)) continue;

						// If the two planets are colliding
						float dist = (p.Displacement - pColliding.Displacement).Length();
						dist = MathF.Max(dist, (p.Radius + pColliding.Radius) / 4);
						if (dist <= pColliding.Radius + p.Radius)
						{
							if (p.IsDebris || p.IsAsteroid)
							{
								toRemove.Add(p);
							}
							else
							{
								continue;
								float distMult = 1 / ((dist - p.Radius) / pColliding.Radius);
								p.Forces += (p.Displacement - pColliding.Displacement).NormalizedCopy() * distMult * distMult * pColliding.Mass / p.Mass * 10000;
							}
						}
					}


					continue;
					if (toRemove.Contains(p)) continue;

					foreach (Planet pColliding in pArr)
					{
						// A planet cannot collide with itself
						if (pColliding == p) continue;
						// To increase performance, debris only needs to be checked once, as it is immediately removed on collisions
						if (pColliding.IsDebris || pColliding.IsAsteroid) continue;
						// Ignore collisions if they are both debris
						if ((p.IsDebris || p.IsAsteroid) && (pColliding.IsDebris || pColliding.IsAsteroid)) continue;

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

					if (!p.IsDebris && !p.IsAsteroid)
					{
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
					}

					p.PrevTimeUpdated = p.TimeExisted;
				}

				if (!(focusPlanet is null))
				{
					cameraPos = -focusPlanet.Displacement * scaleMod;
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
				Planet particle = new Planet(colour, 0.01f, dir * random.Next(600, 700), dir * planet.Radius * 1.1f + planet.Displacement, 10, true)
				{
					// Vary initial time existed so all debris from same collision doesn't disappear at the same time
					TimeExisted = (float)((random.NextDouble() - 0.5) * 2)
				};
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
				if (p.IsDebris || p.IsAsteroid) continue;
				int count = p.PrevPoints.Length;
				for (int i = 0; i < count - 1; i++)
				{
					if (i == p.PointIndex) continue;

					// Note: Not ideal, as it prevents any lines to/from (0, 0)
					// Currently, this fixes the issue of newly-added planets having trails to (0, 0)
					// However, it is extremely unlikely any planet will spend long at *exactly* (0, 0), meaning this is not a serious issue
					if (p.PrevPoints[i + 1] == Vector2.Zero) continue;
					if (p.PrevPoints[i] == Vector2.Zero) continue;

					DrawTrailPoint(p.PrevPoints[i], p.PrevPoints[i + 1], p.Colour, p.PointIndex, i, count);
				}
				
				if (p.PointIndex != p.PrevPoints.Length - 1 && p.HasRepeated) DrawTrailPoint(p.PrevPoints[0], p.PrevPoints[^1], p.Colour, p.PointIndex, p.PrevPoints.Length - 1, count);
			}

			foreach (Planet p in ps)
			{
				if (p.IsDebris || p.IsAsteroid) _spriteBatch.DrawPoint(p.Displacement * scaleMod + cameraPos + windowSize / 2, p.Colour, MathF.Max(0.05f * scaleMod, 1));
				else _spriteBatch.DrawCircle(p.Displacement * scaleMod + cameraPos + windowSize / 2, p.Radius * scaleMod, 20, p.Colour, p.Radius / 5 * scaleMod);
			}
			// Show the user where they're aiming when preparing to add a new planet
			if (preparingToAdd) _spriteBatch.DrawLine(mouseStartPos, mouseCurrentPos, Color.Red);

			//_spriteBatch.DrawString(debugFont, debugText, Vector2.Zero, new Color(200, 200, 200));
			_spriteBatch.DrawString(debugFont, $"Desired Mass: {desiredMass}\nDesired radius: {desiredRadius}", Vector2.Zero, new Color(200, 200, 200));

			_spriteBatch.End();

			base.Draw(gameTime);
		}

		private void DrawTrailPoint(Vector2 p1, Vector2 p2, Color colour, int index, int i, int totalCount)
		{
			i -= index;
			if (i < 0) i += totalCount;
			_spriteBatch.DrawLine(p1 * scaleMod + cameraPos + windowSize / 2, p2 * scaleMod + cameraPos + windowSize / 2, colour * ((float)i / totalCount), 1);
		}
	}
}
