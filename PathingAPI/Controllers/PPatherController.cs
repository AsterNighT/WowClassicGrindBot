﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PPather;
using PPather.Data;
using PPather.Graph;
using SharedLib;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WowTriangles;

namespace PathingAPI.Controllers
{
    public class SearchParameters
    {
        public int FromUIMapId { get; set; }
        public float FromV1 { get; set; }
        public float FromV2 { get; set; }
        public int ToUIMapId { get; set; }
        public float ToV1 { get; set; }
        public float ToV2 { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class PPatherController : ControllerBase
    {
        private readonly PPatherService service;
        private readonly ILogger logger;

        private static bool isBusy;
        private static bool initialised;

        public PPatherController(PPatherService service, ILogger logger)
        {
            this.service = service;
            this.logger = logger;
        }

        /// <summary>
        /// Allows a route to be calculated from one point to another using minimap coords.
        /// </summary>
        /// <remarks>
        /// map1 and map2 are the map ids. See https://wow.gamepedia.com/API_C_Map.GetBestMapForUnit
        ///
        ///     /dump C_Map.GetBestMapForUnit("player")
        ///
        ///     Dump: value=_Map.GetBestMapForUnit("player")
        ///     [1]=1451
        ///
        /// x and y are the map coordinates for the zone (same as the mini map). See https://wowwiki.fandom.com/wiki/API_GetPlayerMapPosition
        ///
        ///     local posX, posY = GetPlayerMapPosition("player");
        /// </remarks>
        /// <param name="map1">from map e.g. 1451</param>
        /// <param name="x1">from X e.g. 46.8</param>
        /// <param name="y1">from Y e.g. 54.2</param>
        /// <param name="map2">to map e.g. 1451</param>
        /// <param name="x2">to X e.g. 51.2</param>
        /// <param name="y2">to Y e.g. 38.9</param>
        /// <returns>A list of x,y,z and mapid</returns>
        [HttpGet("MapRoute")]
        [Produces("application/json")]
        public JsonResult MapRoute(int map1, float x1, float y1, int map2, float x2, float y2)
        {
            isBusy = true;
            service.SetLocations(service.GetWorldLocation(map1, x1, y1), service.GetWorldLocation(map2, x2, y2));
            var path = service.DoSearch(PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);

            var worldLocations = path == null ? new List<WorldMapAreaSpot>() : path.locations.Select(s => service.ToMapAreaSpot(s.X, s.Y, s.Z, map1));
            isBusy = false;
            return new JsonResult(worldLocations, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Allows a route to be calculated from one point to another using world coords.
        /// e.g. -896, -3770, 11, (Barrens,Rachet) to -441, -2596, 96, (Barrens,Crossroads,Barrens)
        /// </summary>
        /// <param name="x1">from X e.g. -896</param>
        /// <param name="y1">from Y e.g. -3770</param>
        /// <param name="z1">from Y e.g. 11</param>
        /// <param name="x2">to X e.g. -441</param>
        /// <param name="y2">to Y e.g. -2596</param>
        /// <param name="z2">from Y e.g. 96</param>
        /// <param name="continent">from ["Azeroth", "Kalimdor", "Northrend", "Expansion01"] e.g. Kalimdor</param>
        /// <returns>A list of x,y,z</returns>
        [HttpGet("WorldRoute")]
        [Produces("application/json")]
        public JsonResult WorldRoute(float x1, float y1, float z1, float x2, float y2, float z2, string continent)
        {
            isBusy = true;
            service.SetLocations(new Location(x1, y1, z1, "l1", continent), new Location(x2, y2, z2, "l2", continent));
            var path = service.DoSearch(PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);
            isBusy = false;
            return new JsonResult(path, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Draws lines on the landscape
        /// Used by the client to show the grind path and the spirit healer path.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        [HttpPost("Drawlines")]
        [Produces("application/json")]
        public bool Drawlines(List<LineArgs> lines)
        {
            if (isBusy) { return false; }
            isBusy = true;
            lines.ForEach(l =>
            {
                var locations = CreateLocations(l);
                if (service.OnLinesAdded != null)
                {
                    service.OnLinesAdded(new LinesEventArgs(l.Name, locations, l.Colour));
                }
            });
            isBusy = false;
            initialised = true;
            return true;
        }

        /// <summary>
        /// Draws spheres on the landscape.
        ///  Used by the client to show the player's location.
        /// </summary>
        /// <param name="sphere"></param>
        /// <returns></returns>
        [HttpPost("DrawSphere")]
        [Produces("application/json")]
        public bool DrawSphere(SphereArgs sphere)
        {
            if (isBusy || !initialised) { return false; }
            isBusy = true;
            var location = service.GetWorldLocation(sphere.MapId, (float)sphere.Spot.X, (float)sphere.Spot.Y);
            if (service.OnSphereAdded != null)
            {
                service.OnSphereAdded(new SphereEventArgs(sphere.Name, location, sphere.Colour));
            }
            isBusy = false;
            return true;
        }

        private List<Location> CreateLocations(LineArgs lines)
        {
            var result = new List<Location>();
            foreach (var s in lines.Spots)
            {
                var location = service.GetWorldLocation(lines.MapId, (float)s.X, (float)s.Y);
                result.Add(location);
            }

            return result;
        }

        /// <summary>
        /// Returns true to indicate that the server is listening.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SelfTest")]
        [Produces("application/json")]
        public JsonResult SelfTest()
        {
            var mpqFiles = MPQTriangleSupplier.GetArchiveNames(DataConfig.Load());

            var countOfMPQFiles = mpqFiles.Where(f => System.IO.File.Exists(f)).Count();

            if (countOfMPQFiles == 0)
            {
                logger.LogInformation("Some of these MPQ files should exist!");
                mpqFiles.ToList().ForEach(l => logger.LogInformation(l));
                logger.LogInformation("No MPQ files found, refer to the Readme to download them.");
            }
            else
            {
                logger.LogInformation("MPQ files exist.");
            }

            return new JsonResult(countOfMPQFiles > 0);
        }

        [HttpPost("DrawPathTest")]
        [Produces("application/json")]
        public bool DrawPathTest()
        {
            string continent = "Azeroth";
            List<float[]> coords = new()
            {
                new float[] {-5609.00f,-479.00f,397.49f },
                new float[] {-5609.33f,-444.00f,405.22f },
                new float[] {-5609.33f,-438.40f,406.02f },
                new float[] {-5608.80f,-427.73f,404.69f },
                new float[] {-5608.80f,-426.67f,404.69f },
                new float[] {-5610.67f,-405.33f,402.02f },
                new float[] {-5635.20f,-368.00f,392.15f },
                new float[] {-5645.07f,-362.67f,385.49f },
                new float[] {-5646.40f,-362.13f,384.69f },
                new float[] {-5664.27f,-355.73f,378.29f },
                new float[] {-5696.00f,-362.67f,366.02f },
                new float[] {-5758.93f,-385.87f,366.82f },
                new float[] {-5782.00f,-394.00f,366.09f }
            };

            if (isBusy) { return false; }
            isBusy = true;

            service.DrawPath(continent, coords);

            isBusy = false;
            return true;
        }
    }
}