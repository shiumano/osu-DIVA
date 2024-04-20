// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Rulesets.Diva.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Diva.UI
{
    /// <summary>
    /// An overlay that captures and displays osu!taiko mouse and touch input.
    /// </summary>
    public partial class DivaTouchInputArea : VisibilityContainer
    {
        // visibility state affects our child. we always want to handle input.
        public override bool PropagatePositionalInputSubTree => true;
        public override bool PropagateNonPositionalInputSubTree => true;

        private KeyBindingContainer<DivaAction> keyBindingContainer = null!;

        private readonly Dictionary<object, DivaAction> trackedActions = new Dictionary<object, DivaAction>();

        private Container mainContent = null!;

        private DrumSegment leftCentre = null!;
        private DrumSegment rightCentre = null!;
        private DrumSegment leftRim = null!;
        private DrumSegment rightRim = null!;

        private readonly Bindable<DivaTouchControlScheme> configTouchControlScheme = new Bindable<DivaTouchControlScheme>();

        [BackgroundDependencyLoader]
        private void load(DivaInputManager taikoInputManager, DivaRulesetConfigManager config)
        {
            Debug.Assert(taikoInputManager.KeyBindingContainer != null);

            keyBindingContainer = taikoInputManager.KeyBindingContainer;

            // Container should handle input everywhere.
            RelativeSizeAxes = Axes.Both;

            const float centre_region = 0.80f;

            Children = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = 350,
                    Y = 20,
                    Masking = true,
                    FillMode = FillMode.Fit,
                    Children = new Drawable[]
                    {
                        mainContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                leftRim = new DrumSegment(DivaAction.Circle)
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomRight,
                                    X = -2,
                                },
                                rightRim = new DrumSegment(DivaAction.Cross)
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomRight,
                                    X = 2,
                                    Rotation = 90,
                                },
                                leftCentre = new DrumSegment(DivaAction.Square)
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomRight,
                                    X = -2,
                                    Scale = new Vector2(centre_region),
                                },
                                rightCentre = new DrumSegment(DivaAction.Triangle)
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomRight,
                                    X = 2,
                                    Scale = new Vector2(centre_region),
                                    Rotation = 90,
                                }
                            }
                        },
                    }
                },
            };

            //config.BindWith(DivaRulesetSettings.TouchControlScheme, configTouchControlScheme);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Hide whenever the keyboard is used.
            Hide();
            return false;
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            handleDown(e.Touch.Source, e.ScreenSpaceTouchDownPosition);
            return true;
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            handleUp(e.Touch.Source);
            base.OnTouchUp(e);
        }

        private static DivaAction[] getOrderedActionsForScheme(DivaTouchControlScheme scheme)
        {
            switch (scheme)
            {
                case DivaTouchControlScheme.Arcade:
                    return new[]
                    {
                        // dousureba ii?
                        DivaAction.Circle,
                        DivaAction.Cross,
                        DivaAction.Square,
                        DivaAction.Triangle
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null);
            }
        }

        private void handleDown(object source, Vector2 position)
        {
            Show();

            DivaAction taikoAction = getDivaActionFromPosition(position);

            // Not too sure how this can happen, but let's avoid throwing.
            if (!trackedActions.TryAdd(source, taikoAction))
                return;

            keyBindingContainer.TriggerPressed(taikoAction);
        }

        private void handleUp(object source)
        {
            keyBindingContainer.TriggerReleased(trackedActions[source]);
            trackedActions.Remove(source);
        }

        private DivaAction getDivaActionFromPosition(Vector2 inputPosition)
        {
            bool centreHit = leftCentre.Contains(inputPosition) || rightCentre.Contains(inputPosition);
            bool leftSide = ToLocalSpace(inputPosition).X < DrawWidth / 2;

            if (leftSide)
                return centreHit ? leftCentre.Action : leftRim.Action;

            return centreHit ? rightCentre.Action : rightRim.Action;
        }

        protected override void PopIn()
        {
            mainContent.FadeIn(500, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            mainContent.FadeOut(300);
        }

        private partial class DrumSegment : CompositeDrawable, IKeyBindingHandler<DivaAction>
        {
            private DivaAction action;

            public DivaAction Action
            {
                get => action;
                set
                {
                    if (action == value)
                        return;

                    action = value;
                    updateColoursFromAction();
                }
            }

            private Circle overlay = null!;

            private Circle circle = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public override bool Contains(Vector2 screenSpacePos) => circle.Contains(screenSpacePos);

            public DrumSegment(DivaAction action)
            {
                this.action = action;

                RelativeSizeAxes = Axes.Both;

                FillMode = FillMode.Fit;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            circle = new Circle
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0.8f,
                                Scale = new Vector2(2),
                            },
                            overlay = new Circle
                            {
                                Alpha = 0,
                                RelativeSizeAxes = Axes.Both,
                                Blending = BlendingParameters.Additive,
                                Scale = new Vector2(2),
                            }
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                updateColoursFromAction();
            }

            public bool OnPressed(KeyBindingPressEvent<DivaAction> e)
            {
                if (e.Action == Action)
                    overlay.FadeTo(1f, 80, Easing.OutQuint);
                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<DivaAction> e)
            {
                if (e.Action == Action)
                    overlay.FadeOut(1000, Easing.OutQuint);
            }

            private void updateColoursFromAction()
            {
                if (!IsLoaded)
                    return;

                var colour = getColourFromDivaAction(Action);

                circle.Colour = colour.Multiply(1.4f).Darken(2.8f);
                overlay.Colour = colour;
            }

            private Color4 getColourFromDivaAction(DivaAction handledAction)
            {
                switch (handledAction)
                {
                    case DivaAction.Circle:
                        return colours.Red;
                    case DivaAction.Cross:
                        return colours.Blue;
                    case DivaAction.Square:
                        return colours.Pink;
                    case DivaAction.Triangle:
                        return colours.Green;
                }

                throw new ArgumentOutOfRangeException();
            }
        }
    }
}
