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
		private float timeMod = 0.1f;
		private int prevPosScale = 1;

		private Vector2 sysBaseVel = new Vector2(0, 0);

		private Vector2 mouseStartPos;
		private Vector2 mouseCurrentPos;
		private bool preparingToAdd = false;

		private float desiredRadius = 0.1f;
		private float desiredMass = 10;

		private float universalG = 1;

		private Planet focusPlanet;
		private Planet drawLinesRelativeTo;

		private Random random = new Random();

		private Keys[] prevKeysDown = { };
		private MouseState prevMState;

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

			GenSystem(1);

			base.Initialize();

			physicsThread = new Thread(PhysicsUpdate) { IsBackground = true };
			physicsThread.Start();
		}

		private void GenPlanetWOrbit(Planet toOrbit, Vector2 diff, Color colour, float radius, float mass, float dir)
		{
			float rOrbit = diff.Length();
			float angle = MathF.Atan(diff.Y / diff.X);
			float vel = MathF.Sqrt(toOrbit.Mass / rOrbit) * dir;
			planets.Add(new Planet(colour, radius, vel * new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) + toOrbit.Velocity, toOrbit.Displacement + diff, mass));
		}

		private void GenAsteroidBelt(float rMin, float rMax, int astCount, Planet around, float dir = 1)
		{
			for (int i = 0; i < astCount; i++)
			{
				float rOrbit = rMin + (float)random.NextDouble() * (rMax - rMin);
				float pos = (float)random.NextDouble() * 2 * MathF.PI;
				float velMag = MathF.Sqrt(around.Mass / rOrbit);
				Vector2 velocity = dir * velMag * new Vector2(-MathF.Sin(pos), MathF.Cos(pos)) + around.Velocity;
				Vector2 displacement = rOrbit * new Vector2(MathF.Cos(pos), MathF.Sin(pos)) + around.Displacement;
				planets.Add(new Planet(Color.Gray, 0.01f, velocity, displacement, 1, false, true));
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

			// TODO: Better way to do this?
			if (!prevKeysDown.Contains(Keys.F1) && kState.IsKeyDown(Keys.F1))
			{
				GenSystem(1);
			}
			else if (!prevKeysDown.Contains(Keys.F2) && kState.IsKeyDown(Keys.F2))
			{
				GenSystem(2);
			}
			else if (!prevKeysDown.Contains(Keys.F3) && kState.IsKeyDown(Keys.F3))
			{
				GenSystem(3);
			}
			else if (!prevKeysDown.Contains(Keys.F4) && kState.IsKeyDown(Keys.F4))
			{
				GenSystem(4);
			}
			else if (!prevKeysDown.Contains(Keys.F5) && kState.IsKeyDown(Keys.F5))
			{
				GenSystem(5);
			}
			else if (!prevKeysDown.Contains(Keys.F12) && kState.IsKeyDown(Keys.F12))
			{
				GenSystem(0);
			}

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

			if (mState.MiddleButton == ButtonState.Pressed && prevMState.MiddleButton == ButtonState.Released)
			{
				Vector2 mPosInSpace = GetMousePosInSpace(mouseCurrentPos);
				Planet nearest = GetNearestPlanetToPoint(mPosInSpace);
				// If the user middle clicks near enough to the planet, it should still count
				float leeway = 16;
				if ((nearest.Displacement - mPosInSpace).LengthSquared() < nearest.Radius * nearest.Radius * leeway)
				{
					if (focusPlanet == nearest) drawLinesRelativeTo = nearest;
					else focusPlanet = nearest;
				}
			}

			// Create debris if the right mouse button is held down
			if (mState.RightButton == ButtonState.Pressed)
			{
				focusPlanet = null;
				//Vector2 position = GetMousePosInSpace(mouseCurrentPos);
				//for (int i = 0; i < 10; i++)
				//{
				//	random.NextUnitVector(out Vector2 velocity);
				//	Planet particle = new Planet(Color.SkyBlue, 0.01f, velocity * random.Next(6, 7) + planets[0].Velocity, position, 1000, true);
				//	planets.Add(particle);
				//}
			}

			prevKeysDown = kState.GetPressedKeys();
			prevMState = mState;
			base.Update(gameTime);
		}

		private void GenSystem(int sysID)
		{
			planets.Clear();
			cameraPos = Vector2.Zero;
			focusPlanet = null;
			drawLinesRelativeTo = null;
			prevPosScale = 1;
			universalG = 1;

			if (sysID == 1)
			{
				GenFakeSystem();
			}
			else if (sysID == 2)
			{
				GenFigEight();
			}
			else if (sysID == 3)
			{
				GenBinarySystem();
			}
			else if (sysID == 4)
			{
				GenSol(false);
			}
			else if (sysID == 5)
			{
				GenSol(true);
			}
		}

		private void GenFakeSystem()
		{
			timeMod = 0.1f;
			sysBaseVel = new Vector2(0, 0);
			scaleMod = 80;
			// Initialise the system with a bunch of planets
			// Sun
			Planet sun = new Planet(Color.Yellow, 0.2f, sysBaseVel, Vector2.Zero, 1000);
			planets.Add(sun);

			GenPlanetWOrbit(sun, new Vector2(1, 0), Color.Red, 0.05f, 05f, 1);

			GenPlanetWOrbit(sun, new Vector2(0, 3.5f), Color.DodgerBlue, 0.075f, 1, 1);
			GenPlanetWOrbit(sun, new Vector2(0, -4.5f), Color.Purple, 0.075f, 1, 1);

			GenPlanetWOrbit(sun, new Vector2(0, -10), Color.Green, 0.1f, 5, 1);
			GenPlanetWOrbit(planets[^1], new Vector2(0, -0.3f), new Color(192, 164, 0), 0.01f, 0.01f, -1f);
			GenAsteroidBelt(0.15f, 0.3f, 50, planets[^2], -1);

			GenPlanetWOrbit(sun, new Vector2(-15, 0), Color.Gold, 0.17f, 5, -1);
			//focusPlanet = planets[^1];
			//drawLinesRelativeTo = planets[^1];
			GenPlanetWOrbit(planets[^1], new Vector2(-0.5f, 0), Color.SkyBlue, 0.01f, 0.01f, 1f);
			GenPlanetWOrbit(planets[^2], new Vector2(0.5f, 0), Color.SkyBlue, 0.01f, 0.01f, -1f);

			// Comet
			planets.Add(new Planet(Color.White, 0.05f, new Vector2(-2, -2) + sysBaseVel, new Vector2(-6, 6), 0.5f));

			GenAsteroidBelt(6, 7, 500, sun);
			GenAsteroidBelt(1.5f, 2.5f, 150, sun);
			GenAsteroidBelt(19, 22, 400, sun);

		}

		private void GenFigEight()
		{
			timeMod = 0.5f;
			sysBaseVel = Vector2.Zero;
			scaleMod = 88;
			Vector2 vel = new Vector2(0.93240737f, 0.86473146f);
			Vector2 pos1 = new Vector2(-0.97000436f, 0.24308753f);
			planets.Add(new Planet(Color.Red, 0.1f, -vel / 2, pos1, 1));
			planets.Add(new Planet(Color.Green, 0.1f, -vel / 2, -pos1, 1));
			planets.Add(new Planet(Color.DodgerBlue, 0.1f, vel, new Vector2(0, 0), 1)); ;
			GenAsteroidBelt(1, 10, 1000, new Planet(Color.Transparent, 0, sysBaseVel, Vector2.Zero, 3), 1);
		}

		private void GenBinarySystem()
		{
			scaleMod = 70;
			sysBaseVel = new Vector2(0, 0);
			timeMod = 0.1f;
			// A star representing the combined mass/displacement of the two binary stars
			Planet starBase = new Planet(Color.Transparent, 0, sysBaseVel, Vector2.Zero, 125);

			GenPlanetWOrbit(starBase, new Vector2(0.75f, 0), Color.Yellow, 0.2f, 500, 1);
			GenPlanetWOrbit(starBase, new Vector2(-0.75f, 0), Color.Yellow, 0.2f, 500, -1);
			starBase.Mass = 1000;

			GenPlanetWOrbit(starBase, new Vector2(0, 4), Color.DeepPink, 0.1f, 1, 1);

			GenPlanetWOrbit(starBase, new Vector2(0, -4), Color.Green, 0.1f, 1, 1);

			GenPlanetWOrbit(starBase, new Vector2(0, -7), Color.Red, 0.15f, 3, 1);

			GenAsteroidBelt(3.5f, 4.5f, 1000, starBase);
		}

		private void GenSol(bool isAccurate)
		{
			// Distances are in km x 10^3
			// Masses are in kg x 10^25

			// TODO: Orbits of moons should be in correct direction
			if (isAccurate)
			{
				prevPosScale = 10000;
				timeMod = 100f;
			}
			else
			{
				prevPosScale = 100000;
				timeMod = 100000f;
			}
			sysBaseVel = Vector2.Zero;
			scaleMod = 0.01f;
			// Update universal g

			bool shouldAddMoons = isAccurate;

			// TODO: Orbits are not perfectly circular in real life

			Planet sun = new Planet(Color.Yellow, 695.7f, Vector2.Zero, Vector2.Zero, 198850);
			planets.Add(sun);

			// Mercury
			GenPlanetWOrbit(sun, new Vector2(55000, 0), Color.Gray, 2.4397f, 0.033011f, 1);

			// Venus
			GenPlanetWOrbit(sun, new Vector2(108000, 0), Color.Orange, 6.052f, 0.48675f, 1);

			// Earth
			GenPlanetWOrbit(sun, new Vector2(150000, 0), Color.Green, 6.371f, 0.597237f, 1);
			// Our Moon
			if (shouldAddMoons) GenPlanetWOrbit(planets[^1], new Vector2(384.4f, 0), Color.Gray, 1.737f, 0.007342f, 1f);

			// Mars
			GenPlanetWOrbit(sun, new Vector2(225000, 0), Color.Red, 3.3895f, 0.064171f, 1);
			// TODO: Without improvements to the physics simulation, Mars' moons are close enough that a lower simulation speed is needed to keep them in orbit
			if (shouldAddMoons)
			{
				// Phobos
				GenPlanetWOrbit(planets[^1], new Vector2(9.376f, 0), Color.Gray, 0.012f, 0.0000000001f, 1);
				// Deimos
				GenPlanetWOrbit(planets[^2], new Vector2(23.4632f, 0), Color.Gray, 0.012f, 0.00000000001f, 1);
			}

			// Jupiter
			GenPlanetWOrbit(sun, new Vector2(775000, 0), Color.SandyBrown, 69.911f, 189.02f, 1);
			if (shouldAddMoons)
			{
				focusPlanet = planets[^1];
				drawLinesRelativeTo = planets[^1];
				// TODO: Moons could probably be different colours
				GenPlanetWOrbit(planets[^1], new Vector2(421.7f, 0), Color.SandyBrown, 1.8216f, 0.008931938f, 1);
				GenPlanetWOrbit(planets[^2], new Vector2(670.9f, 0), Color.SandyBrown, 1.5608f, 0.0048f, 1);
				GenPlanetWOrbit(planets[^3], new Vector2(1070.4f, 0), Color.SandyBrown, 2.6341f, 0.014819f, 1);
				GenPlanetWOrbit(planets[^4], new Vector2(1882.7f, 0), Color.SandyBrown, 2.4103f, 0.01075f, 1);
				// I'm not adding all 80-ish moons

				GenAsteroidBelt(92, 226, 200, planets[^5]);
			}

			// Saturn
			GenPlanetWOrbit(sun, new Vector2(1433000, 0), Color.Beige, 58.232f, 56.834f, 1);
			if (shouldAddMoons)
			{
				// Titan
				GenPlanetWOrbit(planets[^1], new Vector2(1221, 0), Color.Beige, 2.574f, 0.0013452f, 1);
				// Rings
				GenAsteroidBelt(80, 120, 200, planets[^2]);
			}

			// Uranus
			GenPlanetWOrbit(sun, new Vector2(3008000, 0), Color.Aquamarine, 25.362f, 8.681f, 1);
			// TODO: Uranus has moons and rings, but they're minor enough that I'm leaving them for now

			// Neptune
			GenPlanetWOrbit(sun, new Vector2(4500000, 0), Color.CornflowerBlue, 24.622f, 10.2413f, 1);
			if (shouldAddMoons)
			{
				Planet neptune = planets[^1];
				// Triton
				// TODO: Should go in opposite directions to normal moons
				GenPlanetWOrbit(neptune, new Vector2(354.759f, 0), Color.CornflowerBlue, 1.3534f, 0.002139f, 1);

				GenAsteroidBelt(41, 43, 50, neptune);
				GenAsteroidBelt(52, 54, 50, neptune);
				GenAsteroidBelt(62, 64, 50, neptune);

			}

			GenAsteroidBelt(344075f, 448793f, 600, sun, 1);

			GenAsteroidBelt(4487936f, 7479894f, 1000, sun, 1);
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
				float tPassed = watch.ElapsedMilliseconds / 1000f * timeMod;
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
						float f = universalG * p.Mass * pOther.Mass / distSqr;

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
						if (Math.Round(p.TimeExisted / prevPosScale, 3, MidpointRounding.ToZero) > Math.Round(p.PrevTimeUpdated / prevPosScale, 3, MidpointRounding.ToZero))
						{
							p.PointIndex++;
							if (p.PointIndex == p.PrevPoints.Length)
							{
								p.PointIndex = 0;
								p.HasRepeated = true;
							}
						}
						p.PrevPoints[p.PointIndex] = p.Displacement;
						if (!(drawLinesRelativeTo is null))
						{
							p.PrevPoints[p.PointIndex] -= drawLinesRelativeTo.Displacement;
						}
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
				if (p.IsDebris || p.IsAsteroid) _spriteBatch.DrawPoint(p.Displacement * scaleMod + cameraPos + windowSize / 2, p.Colour, Math.Min(MathF.Max(0.05f * scaleMod, 1), 3));
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
			if (!(drawLinesRelativeTo is null))
			{
				p1 += drawLinesRelativeTo.Displacement;
				p2 += drawLinesRelativeTo.Displacement;
			}
			Vector2 point1 = p1 * scaleMod + cameraPos + windowSize / 2;
			Vector2 point2 = p2 * scaleMod + cameraPos + windowSize / 2;
			_spriteBatch.DrawLine(point1, point2, colour * ((float)i / totalCount), 2);
		}
	}
}
