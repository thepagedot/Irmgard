﻿using System;
using System.Linq;

using Android.Support.V4.Content;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;

using IRMGARD.Models;

namespace IRMGARD
{
    public abstract class BaseConceptFragment<T> : LessonFragment<T> where T : Lesson
    {
        protected View CreateConceptView(Concept concept)
        {
            View view;

            var inflater = LayoutInflater.From(Activity.BaseContext);
            if (concept is BaseText)
            {
                var baseText = (concept as BaseText);
                if (baseText.LetterTags != null && baseText.LetterTags.Count > 0)
                {
                    view = ApplyLetterTags(inflater, baseText);
                }
                else
                {
                    view = inflater.Inflate(Resource.Layout.BaseText, null);
                    var tvText = view.FindViewById<TextView>(Resource.Id.tvText);
                    DecorateText(tvText, baseText, new Android.Graphics.Color(
                        ContextCompat.GetColor(Activity.BaseContext, Resource.Color.neon)));
                    SetTextColor(tvText, baseText);
                    AdjustTextSize(tvText, baseText);
                }

                if (baseText is ISound && baseText.SoundPath != null)
                {
                    var speakerDecorator = (ViewGroup)inflater.Inflate(Resource.Layout.BaseTextSpeaker, null);
                    speakerDecorator.AddView(view);
                    view = speakerDecorator;
                }

                if (IsTextCardCallback(concept as BaseText))
                {
                    var cardView = (FrameLayout)inflater.Inflate(Resource.Layout.BaseTextCard, null);
                    cardView.AddView(view);
                    view = cardView;
                }
                else
                {
                    if (!baseText.ShowAsPlainText)
                    {
                        var borderedView = (FrameLayout)inflater.Inflate(Resource.Layout.BaseTextBordered, null);
                        borderedView.AddView(view);
                        view = borderedView;
                    }
                }
            }
            else if (concept is Speaker)
            {
                view = inflater.Inflate(IsSpeakerCardCallback(concept as Speaker) ? Resource.Layout.SpeakerCard : Resource.Layout.Speaker, null);
            }
            else if (concept is Picture)
            {
                view = inflater.Inflate(IsPictureCardCallback(concept as Picture) ? Resource.Layout.PictureCard : Resource.Layout.Picture, null);

                if (concept.ActivateOnSuccess || concept.ActivateOnMistake)
                {
                    (view as ViewGroup).GetChildAt(0).LayoutParameters = new FrameLayout.LayoutParams(ToPx(150), ToPx(150));
                }

                if (!string.IsNullOrEmpty((concept as Picture).ImagePath))
                {
                    var bitmap = BitmapLoader.Instance.LoadBitmap(CountPictureItems(), Activity.BaseContext, (concept as Picture).ImagePath);
                    if (bitmap != null)
                    {
                        var ivPicture = view.FindViewById<ImageView>(Resource.Id.ivPicture);
                        ivPicture.SetImageBitmap(bitmap);
                    }
                }
            }
            else if (concept is Models.Space)
            {
                view = new Android.Widget.Space(Activity.BaseContext);
                view.LayoutParameters = new LinearLayout.LayoutParams(ToPx((concept as Models.Space).Width), ViewGroup.LayoutParams.MatchParent);
            }
            else
            {
                view = inflater.Inflate(Resource.Layout.BaseText, null);
                var tvText = view.FindViewById<TextView>(Resource.Id.tvText);
                tvText.Text = string.Format("Concept type {0} does not exist!", concept.GetType().ToString());
            }

            // Attach concept object to view
            SetTag<Concept>(view, Resource.Id.concept_tag_key, concept);

            // Attach click handler
            view.Click += ConceptView_Click_PlaySound;

            return view;
        }

        protected void ConceptView_Click_PlaySound(object sender, EventArgs e)
        {
            var concept = GetTag<Concept>(sender as View, Resource.Id.concept_tag_key);
            if (concept is ISound && !string.IsNullOrEmpty((concept as ISound).SoundPath))
            {
                // Play sound on click
                SoundPlayer.PlaySound(Activity.BaseContext, (concept as ISound).SoundPath);
            }
        }

        /// <summary>
        /// Implement to return the maximum count of different concepts of type <see cref="Picture"/>
        /// </summary>
        /// <returns>The maximum count of different pictures</returns>
        protected virtual int CountPictureItems()
        {
            return 1;
        }

        protected virtual string GetTextCallback(BaseText concept)
        {
            return concept.Text;
        }

        protected virtual bool IsTextCardCallback(BaseText concept)
        {
            return concept.IsSolution || concept.IsOption || concept.ActivateOnSuccess || concept.ActivateOnMistake;
        }

        protected virtual bool IsSpeakerCardCallback(Speaker concept)
        {
            return concept.IsSolution || concept.IsOption;
        }

        protected virtual bool IsPictureCardCallback(Picture concept)
        {
            return concept.IsSolution || concept.IsOption || concept.ActivateOnSuccess || concept.ActivateOnMistake;
        }

        protected void SetTag<V>(View view, int resKey, V obj)
        {
            view.SetTag(resKey, new JavaObjectWrapper<V>() { Obj = obj });
        }

        protected V GetTag<V>(View view, int resKey)
        {
            return view.GetTag(resKey) != null ? (view.GetTag(resKey) as JavaObjectWrapper<V>).Obj : default(V);
        }

        protected bool IsSmallHeight()
        {
            return (Resources.DisplayMetrics.HeightPixels / Resources.DisplayMetrics.Density) < 550;
        }

        protected int ToPx(int dp)
        {
            return (int)(dp * Resources.DisplayMetrics.Density);
        }

        void DecorateText(TextView tv, BaseText baseText, Android.Graphics.Color color)
        {
            string text = GetTextCallback(baseText);

            if (baseText.Highlights != null && baseText.Highlights.Count > 0)
            {
                var span = new SpannableString(text);
                foreach (var highlight in baseText.Highlights)
                {
                    span.SetSpan(new BackgroundColorSpan(color), highlight[0], highlight[1], SpanTypes.ExclusiveExclusive);
                }
                tv.Append(span);
            }
            else
            {
                tv.Text = text;
            }
        }

        void AdjustTextSize(TextView tvText, BaseText concept)
        {
            if (concept.TextSize > 0)
            {
                tvText.SetTextSize(Android.Util.ComplexUnitType.Dip, concept.TextSize);
            }
            else
            {
                if (concept is Letter)
                {
                    tvText.SetTextSize(Android.Util.ComplexUnitType.Dip, 28);
                }
                else if (concept is Syllable)
                {
                    tvText.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
                }
                else if (concept is Word)
                {
                    tvText.SetTextSize(Android.Util.ComplexUnitType.Dip, 18);
                }
            }
        }

        View ApplyLetterTags(LayoutInflater inflater, BaseText concept)
        {
            string text = GetTextCallback(concept);

            if (concept is Word || concept is Syllable)
            {
                var viewGroup = (ViewGroup)inflater.Inflate(Resource.Layout.BaseTextGroup, null);
                for (int i = 0; i < text.Length; i++)
                {
                    var view = inflater.Inflate(Resource.Layout.BaseText, null);
                    var tvText = view.FindViewById<TextView>(Resource.Id.tvText);
                    tvText.Text = char.ToString(text[i]);
                    SetTextColor(tvText, concept);
                    AdjustTextSize(tvText, concept);
                    EnableIndicator(view, concept, concept.LetterTags[i]);
                    viewGroup.AddView(view);
                }

                return viewGroup;
            }
            else if (concept is Letter)
            {
                var view = inflater.Inflate(Resource.Layout.BaseText, null);
                var tvText = view.FindViewById<TextView>(Resource.Id.tvText);
                tvText.Text = text;
                SetTextColor(tvText, concept);
                AdjustTextSize(tvText, concept);
                EnableIndicator(view, concept, concept.LetterTags.First());

                return view;
            }
            else
            {
                throw new InvalidCastException("Unknown concept of type BaseText!");
            }
        }

        void SetTextColor(TextView tvText, BaseText concept)
        {
            if (!string.IsNullOrEmpty(concept.Color))
            {
                tvText.SetTextColor(Android.Graphics.Color.ParseColor(concept.Color));
            }
        }

        void EnableIndicator(View letterView, BaseText concept, LetterTag letterTag)
        {
            if (letterTag == LetterTag.Short)
            {
                var view = letterView.FindViewById<View>(Resource.Id.shortIndicator);
                SetDrawableColor(view, concept);
                view.Visibility = ViewStates.Visible;
            }
            else if (letterTag == LetterTag.Long)
            {
                var view = letterView.FindViewById<View>(Resource.Id.longIndicator);
                SetDrawableColor(view, concept);
                view.Visibility = ViewStates.Visible;
            }
        }

        void SetDrawableColor(View view, BaseText concept)
        {
            if (!string.IsNullOrEmpty(concept.Color))
            {
                var background = view.Background as Android.Graphics.Drawables.GradientDrawable;
                background.SetColor(Android.Graphics.Color.ParseColor(concept.Color).ToArgb());
            }
        }
    }
}

