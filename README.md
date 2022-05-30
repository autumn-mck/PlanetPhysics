# PlanetPhysics
A small planet simulator built for fun as a side project.

Created using Monogame and Monogame Extended

![Simulation Gif](imgs_md/system.gif "Simulation Gif")
![Stable 3 body figure 8](imgs_md/figure_8.png "Stable 3 body figure 8")
![Our solar system to scale](imgs_md/sol_scale.png "Our solar system to scale")

Currently uses the rather inaccurate Euler method for integration (this is mostly mitigated by using very small step sizes), but ideally it should use something like the Runge-kutta methods for better accuracy.  
Includes several pre-set situations, including a fictional but nice-looking system (shown below), a stable 3-body figure-8 system, a binary star system, and our solar system to scale (with and without moons)
