// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Diva.Objects;
using osuTK;
using System.Threading;
using OpenTabletDriver.Plugin.Output;
using System;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Rulesets.Diva.Beatmaps
{
    public class DivaBeatmapConverter : BeatmapConverter<DivaHitObject>
    {
        //todo:
        //make single position bursts to a line pattern
        //every approach piece of a combo will come from one direction
        //create patterns of same button

        private static readonly Vector2 topLeft = getPosition(new Vector2(0, 0));
        private static readonly Vector2 centre = getPosition(new Vector2(256, 192));
        private static readonly Vector2 bottomRight = getPosition(new Vector2(512, 384));

        private static float width = bottomRight.X - topLeft.X;
        private static float height = bottomRight.Y - topLeft.Y;

        private static class Angles
        {
            public static Vector2 toRight = new Vector2(1, 0);
            public static Vector2 toDownRight = new Vector2(0.9f, 0.6f);
            public static Vector2 toDown = new Vector2(0, 1);
            public static Vector2 toDownLeft = new Vector2(-0.9f, 0.6f);
            public static Vector2 toLeft = new Vector2(-1, 0);
            public static Vector2 toUpLeft = new Vector2(-0.9f, -0.6f);
            public static Vector2 toUp = new Vector2(0, -1);
            public static Vector2 toUpRight = new Vector2(0.9f, -0.6f);
        }

        private Vector2 nowStreamAngle = Angles.toRight;
        private int streamLength = 0;

        public int TargetButtons;
        public bool AllowDoubles = true;

        private int objectIndex = 0;
        private DivaAction prevAction = DivaAction.Triangle;
        private Vector2 prevObjectPos = Vector2.Zero;
        //these variables were at the end of the class, such heresy had i done

        private double prevObjectTime = -1;

        private const float approach_piece_distance = 1200;
        

        public DivaBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
            this.TargetButtons = beatmap.BeatmapInfo.Difficulty.OverallDifficulty switch
            {
                >= 6.0f => 4,
                >= 4.5f => 3,
                >= 2f => 2,
                _ => 1,
            };

            //Console.WriteLine(this.TargetButtons);
        }

        public override bool CanConvert() => true; //Beatmap.HitObjects.All(h => h is IHasPosition);
        // ☝ハァ？？

        protected override IEnumerable<DivaHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            //  //not sure if handling the cancellation is needed, as offical modes doesnt handle *scratches my head* or even its possible
            //  //var pos = (original as IHasPosition)?.Position ?? Vector2.Zero;

            if (prevObjectTime == -1)
                prevObjectTime = original.StartTime;

            var nextObject = beatmap.HitObjects[objectIndex];

            objectIndex++;

            var delta = (original.StartTime - prevObjectTime) / 1000;

            prevObjectTime = original.StartTime;

            var newCombo = (original as IHasCombo)?.NewCombo ?? false;
            var comboFinish = (nextObject as IHasCombo)?.NewCombo ?? false;

            var bpm = BPMSAt(original.StartTime);
            var distance = (int)(delta / (60 / bpm)*120);

            Vector2 pos;
            if (streamLength > 5 && newCombo)
            {
                if (((nowStreamAngle.X < 0 && prevObjectPos.X < 256) || (nowStreamAngle.X > 0 && prevObjectPos.X > 256)) && streamLength < 10) {
                    pos = prevObjectPos;
                    nowStreamAngle = nextAngle();
                    pos += nowStreamAngle * distance;
                    if (!isInsidePlayfield(pos)){
                        pos -= nowStreamAngle * distance;
                        nowStreamAngle = nextAngle();
                        pos += nowStreamAngle * distance;
                    }
                    streamLength++;
                }
                else
                {
                    pos = getPosition((original as IHasPosition)?.Position ?? Vector2.Zero);
                    streamLength = 0;

                    if (pos.X < 256)
                        nowStreamAngle = Angles.toRight;
                    else
                        nowStreamAngle = Angles.toLeft;
                }
            }
            else {
                pos = prevObjectPos;
                pos += nowStreamAngle * distance;
                streamLength++;
            }

            if (!isInsidePlayfield(pos))
            {
                pos = getPosition((original as IHasPosition)?.Position ?? Vector2.Zero);
                streamLength = 0;

                if (pos.X < 256)
                    nowStreamAngle = Angles.toRight;
                else
                    nowStreamAngle = Angles.toLeft;
            }

            Console.WriteLine($"{isInsidePlayfield(pos)}, {bottomRight.X}, {pos.X}");

            //currently press presses are placed in place of sliders as placeholder, but arcade slider are better suited for these
            //another option would be long sliders: arcade sliders, short sliders: doubles
            if (AllowDoubles && original is IHasPathWithRepeats && (!(nextObject is IHasPathWithRepeats) || comboFinish))
            {
                yield return new DoublePressButton
                {
                    Samples = original.Samples,
                    StartTime = original.StartTime,
                    Position = pos,
                    ValidAction = ValidAction(),
                    DoubleAction = DoubleAction(prevAction),
                    ApproachPieceOriginPosition = GetApproachPieceOriginPos(pos),
                };
            }
            else
            {
                yield return new DivaHitObject
                {
                    Samples = original.Samples,
                    StartTime = original.StartTime,
                    Position = pos,
                    ValidAction = ValidAction(),
                    ApproachPieceOriginPosition = GetApproachPieceOriginPos(pos),
                };
            }

        }

        private static DivaAction DoubleAction(DivaAction ac) => ac switch
        {
            DivaAction.Circle => DivaAction.Right,
            DivaAction.Cross => DivaAction.Down,
            DivaAction.Square => DivaAction.Left,
            _ => DivaAction.Up
        };

        double BPMSAt ( double time ) => Beatmap.ControlPointInfo.TimingPointAt( time ).BPM;

        public static double Vector2ToAngle(Vector2 vector)
        {
            return Math.Atan2(vector.Y, vector.X);
        }

        private static Vector2 getPosition(Vector2 originalPos)
        {
            var pos = originalPos;
            if (pos.X < 300)
                pos.X = 0;
            else if (pos.X > 412)
                pos.X = 512;
            
            pos = Vector2.Multiply(pos, 1.5f);
            pos.X += 150;
            return pos;
        }

        private bool isInsidePlayfield(Vector2 pos)
        {
            return topLeft.X <= pos.X && pos.X <= bottomRight.X && topLeft.Y <= pos.Y && pos.Y <= bottomRight.Y;
        }

        private Vector2 nextAngle()
        {
            if (nowStreamAngle == Angles.toRight){
                if (prevObjectPos.Y < 192)
                    return Angles.toDownRight;
                else
                    return Angles.toUpRight;
            } else if (nowStreamAngle == Angles.toUpRight || nowStreamAngle == Angles.toDownRight){
                return Angles.toRight;
            } else if (nowStreamAngle == Angles.toLeft){
                if (prevObjectPos.Y < 192)
                    return Angles.toDownLeft;
                else
                    return Angles.toUpLeft;
            } else if (nowStreamAngle == Angles.toUpLeft || nowStreamAngle == Angles.toDownLeft){
                return Angles.toLeft;
            } else {
                return Angles.toRight;
            }
        }

        //placeholder
        private DivaAction ValidAction()
        {
            var ac = DivaAction.Circle;

            switch (prevAction)
            {
                case DivaAction.Circle:
                    if (this.TargetButtons < 2) break;
                    ac = DivaAction.Cross;
                    break;

                case DivaAction.Cross:
                    if (this.TargetButtons < 3) break;
                    ac = DivaAction.Square;
                    break;

                case DivaAction.Square:
                    if (this.TargetButtons < 4) break;
                    ac = DivaAction.Triangle;
                    break;
            }

            prevAction = ac;
            return ac;
        }

        private Vector2 GetApproachPieceOriginPos(Vector2 currentObjectPos)
        {
            var dir = centre - currentObjectPos;
            prevObjectPos = currentObjectPos;

            // if (Math.Abs(dir.X) > Math.Abs(dir.Y))
            //     dir.Y = 0;
            // else
            //     dir.X = 0;

            dir.X = 0;

            if (dir == Vector2.Zero)
                return new Vector2(approach_piece_distance);

            return dir.Normalized() * approach_piece_distance;
        }
    }
}
