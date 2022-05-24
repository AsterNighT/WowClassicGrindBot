﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Core.Extensions;
using SharedLib.NpcFinder;
using Game;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class NpcNameTargeting
    {
        private const int MOUSE_DELAY = 40;

        private readonly ILogger logger;
        private readonly CancellationTokenSource cts;
        private readonly NpcNameFinder npcNameFinder;
        private readonly IMouseInput input;

        public int NpcCount => npcNameFinder.NpcCount;

        public List<Point> locTargetingAndClickNpc { get; }
        public List<Point> locFindByCursorType { get; }

        public NpcNameTargeting(ILogger logger, CancellationTokenSource cts, NpcNameFinder npcNameFinder, IMouseInput input)
        {
            this.logger = logger;
            this.cts = cts;
            this.npcNameFinder = npcNameFinder;
            this.input = input;


            locTargetingAndClickNpc = new List<Point>
            {
                new Point(0, 0),
                new Point(-10, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(10, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };

            locFindByCursorType = new List<Point>
            {
                new Point(0, 0),
                new Point(0, 25).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(0, 75).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };
        }

        public void ChangeNpcType(NpcNames npcNames)
        {
            npcNameFinder.ChangeNpcType(npcNames);
        }

        public void WaitForUpdate()
        {
            npcNameFinder.WaitForUpdate();
        }


        public void TargetingAndClickNpc(bool leftClick, CancellationTokenSource cts)
        {
            var npc = npcNameFinder.Npcs.First();
            logger.LogInformation($"> NPCs found: ({npc.Min.X},{npc.Min.Y})[{npc.Width},{npc.Height}]");

            foreach (var location in locTargetingAndClickNpc)
            {
                if (cts.IsCancellationRequested)
                    return;

                var clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + location.X, npc.ClickPoint.Y + location.Y);
                input.SetCursorPosition(clickPostion);
                cts.Token.WaitHandle.WaitOne(MOUSE_DELAY);

                if (cts.IsCancellationRequested)
                    return;

                CursorClassifier.Classify(out var cls);
                if (cls == CursorType.Kill || cls == CursorType.Vendor)
                {
                    AquireTargetAtCursor(clickPostion, npc, leftClick);
                    return;
                }
            }
        }

        public bool FindBy(params CursorType[] cursor)
        {
            List<Point> attemptPoints = new List<Point>();

            foreach (var npc in npcNameFinder.Npcs)
            {
                attemptPoints.AddRange(locFindByCursorType);
                foreach(var point in locFindByCursorType)
                {
                    attemptPoints.Add(new Point(npc.Width / 2, point.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight));
                    attemptPoints.Add(new Point(-npc.Width / 2, point.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight));
                }

                foreach (var location in attemptPoints)
                {
                    var clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + location.X, npc.ClickPoint.Y + location.Y);
                    input.SetCursorPosition(clickPostion);
                    cts.Token.WaitHandle.WaitOne(MOUSE_DELAY);
                    CursorClassifier.Classify(out var cls);
                    if (cursor.Contains(cls))
                    {
                        AquireTargetAtCursor(clickPostion, npc);
                        return true;
                    }
                }
                attemptPoints.Clear();
            }
            return false;
        }

        private void AquireTargetAtCursor(Point clickPostion, NpcPosition npc, bool leftClick = false)
        {
            if (leftClick)
                input.LeftClickMouse(clickPostion);
            else
                input.RightClickMouse(clickPostion);

            logger.LogInformation($"{nameof(NpcNameTargeting)}: NPC found! Height={npc.Height}, width={npc.Width}, pos={clickPostion}");
        }

        public void ShowClickPositions(Graphics gr)
        {
            if (NpcCount <= 0)
            {
                return;
            }

            using (var whitePen = new Pen(Color.White, 3))
            {
                npcNameFinder.Npcs.ForEach(n =>
                {
                    locFindByCursorType.ForEach(l =>
                    {
                        gr.DrawEllipse(whitePen, l.X + n.ClickPoint.X, l.Y + n.ClickPoint.Y, 5, 5);
                    });
                });
            }
        }

    }
}
