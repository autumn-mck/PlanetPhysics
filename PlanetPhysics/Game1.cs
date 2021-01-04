using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Framework;
using MonoGame.OpenGL;
using System;
using System.Collections.Generic;

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
		private float scaleMod = 1;
		private int prevScroll = 0;

		private Vector2 mouseStartPos;
		private Vector2 mouseCurrentPos;
		private bool preparingToAdd = false;

		Random random = new Random();

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

			// Initialise the system with a bunch of planets
			// Sun
			planets.Add(new Planet(Color.Yellow, 60, Vector2.Zero, new Vector2(1, 0), 330000));

			// Smaller planets
			planets.Add(new Planet(Color.CornflowerBlue, 10, new Vector2(120, -120), new Vector2(200, 200), 1));
			planets.Add(new Planet(Color.CadetBlue, 10, new Vector2(120, 120), new Vector2(-200, 200), 1));
			planets.Add(new Planet(Color.LightBlue, 10, new Vector2(-120, -120), new Vector2(200, -200), 1));
			planets.Add(new Planet(Color.SkyBlue, 10, new Vector2(-120, 120), new Vector2(-200, -200), 1));

			// Small, heavy planet
			planets.Add(new Planet(Color.SkyBlue, 20, new Vector2(60, -20), new Vector2(-600, 200), 300000));

			base.Initialize();
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

			// Allow the user to zoom in and out
			if (mState.ScrollWheelValue > prevScroll)
			{
				scaleMod *= 1.1f;
			}
			else if (mState.ScrollWheelValue < prevScroll)
			{
				scaleMod /= 1.1f;
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
				
				Vector2 position = (mouseStartPos - cameraPos) / scaleMod;
				
				planets.Add(new Planet(Color.SkyBlue, 10, velocity, position, 1));
				
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

			float tPassed = gameTime.GetElapsedSeconds();

			// Update the time existed and check if debris should be removed
			foreach (Planet p in planets)
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
			foreach (Planet p in planets)
			{
				// If a planet is about to be removed, there is no point in considering where it is going to go
				if (toRemove.Contains(p)) continue;

				Vector2 forces = p.Forces;

				foreach (Planet pOther in planets)
				{
					// A planet cannot apply forces to itself
					if (pOther == p) continue;
					// Ignore forces from debris
					if (pOther.IsDebris) continue;

					float distSqr = (p.Displacement - pOther.Displacement).LengthSquared();

					Vector2 direction = (p.Displacement - pOther.Displacement).NormalizedCopy();

					// Calculate gravity. TODO: Scale is way off
					float f = 30 * p.Mass * pOther.Mass / distSqr;

					forces -= direction * f;
				}

				p.Forces = forces;
			}

			// Deal with collisions
			foreach (Planet p in planets)
			{
				if (toRemove.Contains(p)) continue;

				foreach (Planet pColliding in planets)
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

			foreach (Planet p in planets)
			{
				if (toRemove.Contains(p)) continue;

				// Update velocity
				p.Velocity += p.Forces / p.Mass * tPassed;
				// Update displacement
				p.Displacement += p.Velocity * tPassed;
				// Reset forces
				p.Forces = Vector2.Zero;
			}

			foreach (Planet p in toRemove) planets.Remove(p);
			planets.AddRange(toAdd);

			toRemove.Clear();
			toAdd.Clear();

			base.Update(gameTime);
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
			GraphicsDevice.Clear(new Color(20, 20, 20, 0));

			_spriteBatch.Begin();

			// Draw all planets
			foreach (Planet p in planets)
			{
				if (p.IsDebris) _spriteBatch.DrawPoint(p.Displacement * scaleMod + cameraPos, p.Colour, MathF.Max(2 * scaleMod, 1));
				else _spriteBatch.DrawCircle(p.Displacement * scaleMod + cameraPos, p.Radius * scaleMod, 20, p.Colour, 4 * scaleMod);
			}

			// Show the user where they're aiming when preparing to add a new planet
			if (preparingToAdd) _spriteBatch.DrawLine(mouseStartPos, mouseCurrentPos, Color.Red);

			_spriteBatch.DrawString(debugFont, debugText, Vector2.Zero, new Color(200, 200, 200));

			_spriteBatch.End();

			base.Draw(gameTime);
		}
	}
}
