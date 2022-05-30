﻿using Microsoft.Extensions.Logging;
using PPather.Graph;
using System;
using WowTriangles;

namespace PPather
{
    public class Search
    {
        public PathGraph PathGraph { get; set; }
        public string continent;

        private readonly DataConfig dataConfig;
        private readonly ILogger logger;

        public Location locationFrom { get; set; }
        public Location locationTo { get; set; }

        private const float toonHeight = 2.0f;
        private const float toonSize = 0.5f;

        private static DateTime startTime;


        public Search(string continent, ILogger logger, DataConfig dataConfig)
        {
            this.logger = logger;
            this.continent = continent;
            this.dataConfig = dataConfig;

            if (PathGraph == null)
            {
                CreatePathGraph(continent);
            }
        }

        public Location CreateLocation(float x, float y, float z = 0)
        {
            // find model 0 i.e. terrain
            var z0 = GetZValueAt(x, y, new int[] { (int)z });

            // if no z value found then try any model
            if (z0 == float.MinValue) { z0 = GetZValueAt(x, y, null); }

            if (z0 == float.MinValue) { z0 = 0; }

            return new Location(x, y, z0 - toonHeight, "", continent);
        }

        private float GetZValueAt(float x, float y, int[] allowedModels)
        {
            float z0 = float.MinValue, z1;
            int flags;

            if (allowedModels != null)
            {
                PathGraph.triangleWorld.FindStandableAt1(x, y, -1000, 2000, out z1, out flags, toonHeight, toonSize, true, null);
            }

            if (PathGraph.triangleWorld.FindStandableAt1(x, y, -1000, 2000, out z1, out flags, toonHeight, toonSize, true, allowedModels))
            {
                z0 = z1;
                // try to find a standable just under where we are just in case we are on top of a building.
                if (PathGraph.triangleWorld.FindStandableAt1(x, y, -1000, z0 - toonHeight - 1, out z1, out flags, toonHeight, toonSize, true, allowedModels))
                {
                    z0 = z1;
                }
            }
            else
            {
                return float.MinValue;
            }

            return z0;
        }

        public void CreatePathGraph(string continent)
        {
            MPQTriangleSupplier mpq = new MPQTriangleSupplier(this.logger, dataConfig);
            mpq.SetContinent(continent);
            var triangleWorld = new ChunkedTriangleCollection(512, this.logger);
            triangleWorld.SetMaxCached(512);
            triangleWorld.AddSupplier(mpq);
            PathGraph = new PathGraph(continent, triangleWorld, null, this.logger, dataConfig);
            this.continent = continent;
            startTime = DateTime.UtcNow;
        }

        public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
        {
            //create a new path graph if required
            const int ResetAfterMinutes = 15;
            if (PathGraph == null || this.continent != locationFrom.Continent || (DateTime.UtcNow - startTime).TotalMinutes >= ResetAfterMinutes)
            {
                CreatePathGraph(locationFrom.Continent);
            }

            PathGraph.SearchEnabled = true;

            // tell the pathgraph which type of search to do
            PathGraph.searchScoreSpot = searchType;

            //slow down the search if required.
            PathGraph.sleepMSBetweenSpots = 0;

            try
            {
                return PathGraph.CreatePath(locationFrom, locationTo, 5, null);
            }
            catch(Exception ex)
            {
                logger.LogError(ex.Message);
                return null;
            }
        }
    }
}