﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Graphics;
using Path = System.IO.Path;
using Android.OS;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Text.Style;
using System.Threading.Tasks;
using Android.Views;
using Android.Widget;
using IRMGARD.Models;
using IRMGARD.Shared;
using AlertDialog = Android.Support.V7.App.AlertDialog;
using Toolbar = Android.Support.V7.Widget.Toolbar;
using Android.Support.Design.Widget;
using Android.Views.Animations;

namespace IRMGARD
{
    [Activity(Label = "Lesson", ParentActivity = typeof(ModuleSelectActivity))]
    public class LessonFameActivity : AppCompatActivity
    {
        private const string lessonFragmentTag = "current-Lesson-fragment";

        IMenu topMenu;
        ImageView ivGoBack;
        ImageView ivGoFwd;
        FloatingActionButton btnNext;
        TextView txtCapitalAlphabet;
        TextView txtLowerAlphabet;
        FrameLayout fragmentContainer;
        RecyclerView rvProgress;
        List<Progress> progressList;
        LessonFragment currentFragment;
        ImageView ivBadge;
        ImageView ivIrmgard;

        List<string> praiseFilesAvail;
        List<string> criticismFilesAvail;
        bool isPlayingPraiseOrCriticism;

        Common Common { get { return DataHolder.Current.Common; } }

        protected override void OnCreate(Bundle bundle)
        {
            ApplyLevelColors();
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.LessonFrame);
            SetSupportActionBar(FindViewById<Toolbar>(Resource.Id.toolbar));
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
            progressList = new List<Progress>();

            ivGoBack = FindViewById<ImageView>(Resource.Id.ivGoBack);
            ivGoBack.Click += ((sender, e) => PreviousLesson());
            ivGoFwd = FindViewById<ImageView>(Resource.Id.ivGoFwd);
            ivGoFwd.Click += ((sender, e) => NextLesson(true, false));
            btnNext = FindViewById<FloatingActionButton>(Resource.Id.btnNext);
            btnNext.Click += BtnNext_Click;

            rvProgress = FindViewById<RecyclerView>(Resource.Id.rvProgress);
            rvProgress.SetLayoutManager(new LinearLayoutManager(this, LinearLayoutManager.Horizontal, false));
            rvProgress.SetAdapter(new ProgressAdapter(progressList,
                Color.ParseColor(DataHolder.Current.CurrentModule.Color),
                Resources.DisplayMetrics.Density));
            txtCapitalAlphabet = FindViewById<TextView>(Resource.Id.txtCapitalAlphabet);
            txtLowerAlphabet = FindViewById<TextView>(Resource.Id.txtLowerAlphabet);
            fragmentContainer = FindViewById<FrameLayout>(Resource.Id.fragmentContainer);
            ivBadge = FindViewById<ImageView>(Resource.Id.ivBadge);
            ivIrmgard = FindViewById<ImageView>(Resource.Id.ivIrmgard);
            ivIrmgard.Click += (s, e) => PlayOrStopInstruction();

            // Initially hide success image
            ivBadge.Visibility = ViewStates.Gone;

            praiseFilesAvail = Assets.List(Path.Combine(Common.AssetSoundDir, Common.AssetPraiseDir)).Select(s => Path.Combine(Common.AssetPraiseDir, s)).ToList();
            criticismFilesAvail = Assets.List(Path.Combine(Common.AssetSoundDir, Common.AssetCriticismDir)).Select(s => Path.Combine(Common.AssetCriticismDir, s)).ToList();
        }

        protected override void OnStart()
        {
            InitLesson();
            base.OnStart();
        }

        protected override void OnPause()
        {
            base.OnPause();

            if (SoundPlayer.IsPlaying)
            {
                SoundPlayer.Stop();
            }
        }

        /// <summary>
        /// Initiates the view with the current lession.
        /// </summary>
        private void InitLesson()
        {
            var module = DataHolder.Current.CurrentModule;
            var lesson = DataHolder.Current.CurrentLesson;
            var iteration = DataHolder.Current.CurrentIteration;

            CheckHintButton();

            // ----------------------------------------------------------------------
            // Module specifics
            // ----------------------------------------------------------------------

            // Set playground background color
            fragmentContainer.SetBackgroundColor(Color.ParseColor(module.Color));

            // ----------------------------------------------------------------------
            // Lesson specifics
            // ----------------------------------------------------------------------

            // Set the name of the current Lesson as page title
            Title = lesson.Title;

            // Hide hint button, if no hint is available
            //if (hintButton != null)
            //hintButton.SetVisible(!string.IsNullOrEmpty(Lesson.Hint));

            // Progress
            ProgressListRefresh(lesson);

            // ----------------------------------------------------------------------
            // Load Lesson fragment
            // ----------------------------------------------------------------------

            // Create an instance of the fragment according to the current type of level
            var transaction = FragmentManager.BeginTransaction();
            currentFragment = CreateFragmentForLesson(DataHolder.Current.CurrentLesson);
            if (currentFragment != null)
            {
                // Handle LessonFragment events
                currentFragment.ProgressListRefreshRequested += LessonFragment_ProgressListRefreshRequested;
                currentFragment.LessonFinished += LessonFragment_LessonFinished;
                currentFragment.IterationFinished += Fragment_IterationFinished;
                currentFragment.IterationChanged += Fragment_IterationChanged;
                currentFragment.UserInteracted += CurrentFragment_UserInteracted;

                // Add the fragment to the container
                transaction.Replace(Resource.Id.fragmentContainer, currentFragment, lessonFragmentTag);
                transaction.Commit();
            }
            else
            {
                // Fragment for this type of Lesson could not be loaded. Remove old fragment
                var oldFragment = FragmentManager.FindFragmentByTag(lessonFragmentTag);
                if (oldFragment != null)
                {
                    transaction.Remove(oldFragment);
                    transaction.Commit();
                }
            }

            // Play instruction
            SoundPlayer.Stop();
            bool waitForCompletion = false;
            if (DataHolder.Current.CurrentLesson.IsRecurringTask)
            {
                var recurringTaskSoundFile = Common.RecurringTaskSoundFiles.PickRandomItems(1).FirstOrDefault();
                if (recurringTaskSoundFile != null)
                {
                    SoundPlayer.PlaySound(this, recurringTaskSoundFile);
                    waitForCompletion = true;
                }
            }
            SoundPlayer.PlaySound(this, waitForCompletion, DataHolder.Current.CurrentLesson.SoundPath);
        }

        private void ProgressListRefresh(Lesson lesson)
        {
            progressList.Clear();
            for (var i = 0; i < lesson.Iterations.Count; i++)
                progressList.Add(new Progress(lesson.Iterations[i].Status)); // Set iteration's status to the same as progess's staus
            progressList[0].IsCurrent = true;

            rvProgress.GetAdapter().NotifyDataSetChanged();
        }

        private void LessonFragment_ProgressListRefreshRequested(object sender, ProgressListRefreshRequestedEventArgs e)
        {
            ProgressListRefresh(e.Lesson);
        }

        private void CurrentFragment_UserInteracted(object sender, UserInteractedEventArgs e)
        {
            if (e.IsReady)
            {
                btnNext.Enabled = true;
                btnNext.Clickable = true;
                btnNext.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.ShowNextButton));
            }
        }

        void Fragment_IterationChanged(object sender, IterationChangedEventArgs e)
        {
            var iterationIndex = DataHolder.Current.CurrentLesson.Iterations.IndexOf(e.Iteration);

            // Update progress
            progressList.ElementAt(iterationIndex).IsCurrent = true;
            rvProgress.GetAdapter().NotifyItemChanged(iterationIndex);

            // Disable check button
            btnNext.Clickable = false;
            btnNext.Enabled = false;
            btnNext.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.HideNextButton));

            // Mark letters in alphabet
            txtCapitalAlphabet.TextFormatted = GetLettersMarked(e.Iteration.LettersToLearn, true);
            txtLowerAlphabet.TextFormatted = GetLettersMarked(e.Iteration.LettersToLearn, false);
        }

        /// <summary>
        /// Handles the Lesson fragement's iteration finished event
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        async void Fragment_IterationFinished(object sender, IterationFinishedEventArgs e)
        {
            // Update status
            var iterationIndex = DataHolder.Current.CurrentLesson.Iterations.IndexOf(e.Iteration);
            progressList.ElementAt(iterationIndex).Status = e.Success ? IterationStatus.Success : IterationStatus.Failed;
            progressList.ElementAt(iterationIndex).IsCurrent = false;
            rvProgress.GetAdapter().NotifyItemChanged(iterationIndex);

            // Show success animation
            if (e.ProvideFeedback)
            {
                if (e.Success)
                    ivBadge.SetImageResource(Resource.Drawable.ic_irmgard_icon_spiel_richtig_neu);
                else
                    ivBadge.SetImageResource(Resource.Drawable.ic_irmgard_icon_spiel_falsch);

                var animation = AnimationUtils.LoadAnimation(this, Resource.Animation.ShowFeedbackIcon);
                animation.AnimationEnd += (s, args) => ivBadge.Visibility = ViewStates.Gone;
                ivBadge.Visibility = ViewStates.Visible;
                ivBadge.StartAnimation(animation);
            }

            // Stop Player
            SoundPlayer.Stop();

            // Save changes
            await DataHolder.Current.SaveProgressAsync();
        }

        /// <summary>
        /// Handles the Lesson fragment's Lesson finished event
        /// </summary>
        /// <param name="sender">sender.</param>
        /// <param name="e">event args.</param>
        async void LessonFragment_LessonFinished(object sender, LessonFinishedEventArgs e)
        {
            if (SoundPlayer.IsPlaying)
                SoundPlayer.Stop();

            var success = DataHolder.Current.CurrentLesson.Iterations.All(i => i.Status == IterationStatus.Success);

            // Play random praise or criticism audio depending on lesson success status
            if (e.ProvideFeedback)
            {
                isPlayingPraiseOrCriticism = true;
                SoundPlayer.PlaySound(this,
                    success
                    ? praiseFilesAvail.PickRandomItems(1).FirstOrDefault()
                    : criticismFilesAvail.PickRandomItems(1).FirstOrDefault());
            }
            while (SoundPlayer.IsPlaying)
            {
                await Task.Delay(500);
            }
            isPlayingPraiseOrCriticism = false;

            NextLesson(success, e.ProvideFeedback);
        }

        /// <summary>
        /// Returns a new instance of the fragment type according to the type of Lesson
        /// </summary>
        /// <returns>The fragment for the Lesson.</returns>
        /// <param name="lesson">current Lesson.</param>
        private LessonFragment CreateFragmentForLesson(Lesson lesson)
        {
            if (lesson is HearMe)
                return new HearMeFragment();
            if (lesson is FourPictures)
                return new FourPicturesFragment();
            if (lesson is PickSyllable)
                return new PickSyllableFragment();
            if (lesson is BuildSyllable)
                return new BuildSyllableFragment();
            if (lesson is FindMissingLetter)
                return new FindMissingLetterFragment();
            if (lesson is AbcRank)
                return new AbcRankFragment();
            if (lesson is LetterDrop)
                return new LetterDropFragment();
            if (lesson is HearTheLetter)
                return new HearTheLetterFragment();
            if (lesson is HearMeAbc)
                return new HearMeAbcFragment();
            if (lesson is Memory)
                return new MemoryFragment();
            if (lesson is LetterWrite)
                return new LetterWriteFragment();

            return null;
        }

        #region UI Operations

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (isPlayingPraiseOrCriticism)
            {
                // User clicked on the next button to skip praise or criticism audio
                SoundPlayer.Stop();
            }
            else
            {
                // Diable button to prevent double click
                btnNext.Enabled = false;
                btnNext.Clickable = false;
                btnNext.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.HideNextButton));

                // Check soution
                currentFragment.CheckSolution();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            topMenu = menu;
            MenuInflater.Inflate(Resource.Menu.levelFrame_menu, topMenu);
            CheckHintButton();

            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                // Play voice instruction
                case Resource.Id.btnVoiceInstruction:
                    PlayOrStopInstruction();
                    break;
                // Show hint
                case Resource.Id.btnHint:
                    if (!string.IsNullOrEmpty(DataHolder.Current.CurrentLesson.Hint))
                    {
                        if (SoundPlayer.IsPlaying)
                        {
                            SoundPlayer.Stop();
                        }
                        SoundPlayer.PlaySound(this, DataHolder.Current.CurrentLesson.Hint);
                    }
                    break;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void PlayOrStopInstruction()
        {
            if (SoundPlayer.IsPlaying)
            {
                SoundPlayer.Stop();
            }
            else
            {
                SoundPlayer.PlaySound(this, DataHolder.Current.CurrentLesson.SoundPath);
            }
        }

        #endregion

        /// <summary>
        /// Switches to the next Lesson if available
        /// </summary>
        private void NextLesson(bool success, bool showFeedbackBadge = true)
        {
            if (showFeedbackBadge)
            {
                // Show success or failure badge
                if (success)
                    ivBadge.SetImageResource(Resource.Drawable.irmgard_icon_spiel_supergemacht);
                else
                    ivBadge.SetImageResource(Resource.Drawable.irmgard_icon_spiel_nochmal);

                var badgeAnimation = AnimationUtils.LoadAnimation(this, Resource.Animation.ShowFeedbackIcon);
                badgeAnimation.AnimationEnd += (s2, args2) =>
                {
                    ivBadge.Visibility = ViewStates.Gone;
                    Next();
                    fragmentContainer.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.SwipeInRight));
                };

                ivBadge.Visibility = ViewStates.Visible;
                ivBadge.StartAnimation(badgeAnimation);
            }
            else
            {
                Next();
                fragmentContainer.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.SwipeInRight));
            }
        }

        private void Next()
        {
            var nextLesson = DataHolder.Current.CurrentModule.GetNextLesson(DataHolder.Current.CurrentLesson);
            if (nextLesson != null)
            {
                DataHolder.Current.CurrentLesson = nextLesson;
                DataHolder.Current.CurrentIteration = nextLesson.Iterations.First();
                InitLesson();
            }
            else
            {
                Finish();
            }
        }

        /// <summary>
        /// Swtiches to the previous Lesson if available.
        /// </summary>
        private void PreviousLesson()
        {
            var previousLesson = DataHolder.Current.CurrentModule.GetPrevioustLesson(DataHolder.Current.CurrentLesson);
            if (previousLesson != null)
            {
                DataHolder.Current.CurrentLesson = previousLesson;
                DataHolder.Current.CurrentIteration = previousLesson.Iterations.First();
                InitLesson();
            }

            fragmentContainer.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.SwipeInLeft));
        }

        public static SpannableString GetLettersMarked(List<string> markedLetters, bool capitalize)
        {
            var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (capitalize == false)
                alphabet = alphabet.ToLower();

            if (markedLetters == null)
                return new SpannableString(alphabet);

            var levelColor = Color.ParseColor(DataHolder.Current.CurrentLevel.Color);
            var spannable = new SpannableString(alphabet);
            foreach (var letter in markedLetters)
            {
                var index = Alphabet.Letters.IndexOf(letter.ToUpper());
                spannable.SetSpan(new ForegroundColorSpan(levelColor), index, index + 1, SpanTypes.ExclusiveExclusive);
            }

            return spannable;
        }

        private void ApplyLevelColors()
        {
            var levelNumber = DataHolder.Current.Levels.IndexOf(DataHolder.Current.CurrentLevel) + 1;
            switch (levelNumber)
            {
                case 1:
                    Theme.ApplyStyle(Resource.Style.Level1Colors, true);
                    break;
                default:
                    break;
            }
        }

        private void CheckHintButton()
        {
            if (topMenu != null)
            {
                var hintButton = topMenu.FindItem(Resource.Id.btnHint);
                if (hintButton != null)
                {
                    var isVisible = !string.IsNullOrEmpty(DataHolder.Current.CurrentLesson.Hint);
                    hintButton.SetVisible(isVisible);
                }
            }
        }
    }
}
