﻿using System;
using IRMGARD.Models;
using System.Collections.Generic;
using System.Linq;
using Android.Views;
using Android.OS;
using Android.Widget;
using Android.Graphics;
using Android.Content;

namespace IRMGARD
{
    public class BuildSyllableFragment : LessonFragment<BuildSyllable>
    {
        LinearLayout llTaskItems;
        LinearLayout llSoundItems;
        FlowLayout flLetters;
        ImageButton btnCheck;

        public BuildSyllableFragment(Lesson lesson) : base(lesson) {}

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            View view = inflater.Inflate(Resource.Layout.BuildSyllable, container, false);

            if (view != null) {
                llTaskItems = view.FindViewById<LinearLayout>(Resource.Id.llTaskItems);
                llSoundItems = view.FindViewById<LinearLayout>(Resource.Id.llSoundItems);
                flLetters = view.FindViewById<FlowLayout> (Resource.Id.flLetters);
                btnCheck = view.FindViewById<ImageButton>(Resource.Id.btnCheck);
                btnCheck.Click += BtnCheck_Click;
			}

            InitIteration ();
            return view;
        }

        protected override void InitIteration()
        {
            base.InitIteration();

            var currentIteration = GetCurrentIteration<BuildSyllableIteration>();

            BuildSyllableSoundElements(currentIteration.Syllables);
            BuildSyllableLetters(currentIteration.Syllables);
            BuildSyllables(currentIteration.Options);


            btnCheck.Enabled = false;
        }

        void PlaySoundOnImageClick (object sender, EventArgs e)
        {
            LinearLayout layout = (sender as LinearLayout);

            if (layout != null)
            {
                int index = ((ViewGroup)layout.Parent).IndexOfChild(layout);
                if (index >= 0)
                {
                    SoundPlayer.PlaySound(Activity.BaseContext, GetCurrentIteration<BuildSyllableIteration>().Syllables.ElementAt(index).SoundPath);
                }
            }

        }

        private void BuildSyllableSoundElements(List<Syllable> syllables)
        {
            foreach (var syllable in syllables)
            {
                var addMultiIcon = false;
                var index = syllables.IndexOf(syllable);
                if (index > -1 && syllables.Count > 1 && (index + 1 < syllables.Count))
                {
                    addMultiIcon = true;
                } 

                var mediaElementAdapter = new BuildSyllableMediaElementAdapter(Activity.BaseContext, 0, syllables, addMultiIcon);


                var view = mediaElementAdapter.GetView(0, null, null);  
                view.Click += PlaySoundOnImageClick;
                // Add letter to view
                llSoundItems.AddView(view);
            }

        }

        private void BuildSyllableLetters(List<Syllable> syllables)
        {
            llTaskItems.RemoveAllViews();
            foreach (var syllable in syllables)
            {
                var addMultiIcon = false;
                var index = syllables.IndexOf(syllable);
                if (index > -1 && syllables.Count > 1 && (index + 1 < syllables.Count))
                {
                    addMultiIcon = true;
                }

                var syllableItemsAdapter = new BuildSyllableTaskItemAdapter(Activity.BaseContext, 0, syllable.SyllableParts, addMultiIcon);

                for (int i = 0; i < syllable.SyllableParts.Count; i++)
                {
                    var view = syllableItemsAdapter.GetView(i, null, null);
                    view.Drag += View_Drag;    

                    // Add letter to view
                    llTaskItems.AddView(view);
                }   
            }
        }

        private void BuildSyllables(List<BuildSyllableOption> options)
        {
            var buildSyllableAdapter = new BuildSyllableAdapter(Activity.BaseContext, 0, options);
            for (int i = 0; i < options.Count; i++) {
                // Add letter to view
                var view = buildSyllableAdapter.GetView (i, null, null);
                var letter = options.ElementAt (i).Letter;

                view.Touch += (sender, e) => {
                    var data = ClipData.NewPlainText ("letter", letter);
                    (sender as View).StartDrag (data, new View.DragShadowBuilder (sender as View), null, 0);
                };

                flLetters.AddView (view);
            }
        }

        void View_Drag (object sender, View.DragEventArgs e)
        {
            // React on different dragging events
            var evt = e.Event;
            switch (evt.Action) 
            {
                case DragAction.Ended:  
                case DragAction.Started:
                    e.Handled = true;
                    break;           

                    // Dragged element enters the drop zone
                case DragAction.Entered:
                    break;

                    // Dragged element exits the drop zone
                case DragAction.Exited:                                       
                    break;

                    // Dragged element has been dropped at the drop zone
                case DragAction.Drop:
                    e.Handled = true;

                    // Try to get clip data
                    var data = e.Event.ClipData;
                    if (data != null)
                    {
                        var taskLetters = GetCurrentIteration<BuildSyllableIteration>().Syllables;
                        var draggedLetter = data.GetItemAt(0).Text;
                        var position = llTaskItems.IndexOfChild(sender as View);

                        var index = position;
                        foreach (var taskLetter in taskLetters)
                        {
                            if (taskLetter.SyllableParts.Count > index)
                            {
                                taskLetter.SyllableParts.ElementAt(index).DraggedLetter = draggedLetter;
                                break;
                            }
                            else
                            {
                                index -= taskLetter.SyllableParts.Count;
                            }
                        }

                        BuildSyllableLetters(taskLetters);
                    }

                    btnCheck.Enabled = true;
                    break;
            }
        }        

        void BtnCheck_Click (object sender, EventArgs e)
        {
            CheckSolution();
        }

        protected override void CheckSolution()
        {
            var isCorrect = true;
            var currentIteration = GetCurrentIteration<BuildSyllableIteration>();

            foreach (var syllables in currentIteration.Syllables)
            {
                foreach (var syllable in syllables.SyllableParts)
                {
                    if (!syllable.Letter.Equals(syllable.DraggedLetter))
                        isCorrect = false;

                    if (!isCorrect)
                        break;
                }
            }

            if (isCorrect)
                FinishIteration();
            else
                Toast.MakeText(Activity.BaseContext, "Leider verloren", ToastLength.Short).Show();
        }
    }
}

