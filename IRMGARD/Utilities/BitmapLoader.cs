﻿using System;
using System.Collections.Generic;
using System.Linq;

using Android.Graphics;
using Android.Content;
using Android.Widget;
using Android.OS;
using Android.Util;

namespace IRMGARD
{
    /// <summary>
    /// Represents a bitmap image loader facility to reduce the memory usage and the amount of GC events
    /// by loading new bitmaps into the space of bitmaps used before.
    ///
    /// Re-using the allocated memory of an old image is only applicable for equally-sized or smaller images.
    ///
    /// To reuse the allocated memory of the bitmaps used before you have to limit maxPoolSize to the maximum
    /// amount of images used in the current lesson. When max pool size limit is reached the next new image
    /// will reuse the allocated memory of the oldest image in the pool.
    ///
    /// To finally release the allocated memory use <see cref="ReleaseCache"/>.
    /// The best time to release allocated bitmap memory is at the end of each Lesson if the subsequent Lesson
    /// is using a smaller amount of bitmaps per iteration than the previous one.
    /// </summary>
    public sealed class BitmapLoader
    {
        const string TAG = "BitmapCache";
        const string AssetImageDir = "Images";

        static readonly BitmapLoader instance = new BitmapLoader();

        readonly BitmapFactoryOptionsPool bitmapPool;

        private BitmapLoader() {
            bitmapPool = new BitmapFactoryOptionsPool();
        }

        public static BitmapLoader Instance
        {
            get { return instance; }
        }

        /// <summary>
        /// Loads a non-scaled bitmap from the Assets folder into memory.
        /// </summary>
        /// <returns>The bitmap.</returns>
        /// <param name="maxPoolSize">The maximum limit of images to pool.</param>
        /// <param name="context">A context to get an AssetManager instance.</param>
        /// <param name="fileName">The image file path.</param>
        /// <param name="assetImageDir">The parent directory for <paramref name="fileName"/> path parameter.</param>
        public Bitmap LoadBitmap(int maxPoolSize, Context context, string fileName,
            string assetImageDir = AssetImageDir)
        {
            Bitmap bitmap = DecodeBitmap(maxPoolSize, context, fileName, assetImageDir);
            if (Env.Debug)
            {
                Log.Debug(TAG, "Sync. Decoding ({0}) done. Bytes:{1}", bitmap.ToString(), bitmap.ByteCount.ToString());
                TimeProfiler.LogMemInfo();
            }
            return bitmap;
        }

        /// <summary>
        /// Loads a non-scaled bitmap from the Assets folder on a background thread into an ImageView.
        /// </summary>
        /// <param name="maxPoolSize">The maximum limit of images to pool.</param>
        /// <param name="imageView">The ImageView for the bitmap to load into.</param>
        /// <param name="context">A context to get an AssetManager instance.</param>
        /// <param name="fileName">The image file path.</param>
        /// <param name="assetImageDir">The parent directory for <paramref name="fileName"/> path parameter.</param>
        public void LoadBitmapInImageViewAsync(int maxPoolSize, ImageView imageView, Context context, string fileName,
            string assetImageDir = AssetImageDir)
        {
            BitmapWorkerTask task = new BitmapWorkerTask(this, imageView);
            task.Execute(maxPoolSize, context, fileName, assetImageDir);
        }

        public void ReleaseCache()
        {
            bitmapPool.ReleaseCache();
        }

        Bitmap DecodeBitmap(int maxPoolSize, Context context, string fileName, string assetImageDir)
        {
            string filePath = System.IO.Path.Combine(assetImageDir, fileName);

            BitmapFactory.Options options = null;
            if (bitmapPool.TryGetOptions(maxPoolSize, filePath, out options))
            {
                return options.InBitmap;
            }

            long started = TimeProfiler.Start();
            if (options.InBitmap == null)
            {
                // First decode with inJustDecodeBounds=true to check dimensions
                options.InJustDecodeBounds = true;
                using (var stream = context.Assets.Open(filePath))
                {
                    BitmapFactory.DecodeStream(stream, null, options);
                }
                Bitmap bitmap = Bitmap.CreateBitmap(options.OutWidth, options.OutHeight, options.InPreferredConfig);
                options.InJustDecodeBounds = false;
                options.InBitmap = bitmap;
                options.InSampleSize = 1;
            }

            // Decode bitmap with inSampleSize set
            using (var stream = context.Assets.Open(filePath))
            {
                var bitmap = BitmapFactory.DecodeStream(stream, null, options);
                TimeProfiler.StopAndLog(TAG, "Decode Stream", started);
                return bitmap;
            }
        }

        class BitmapFactoryOptionsPool
        {
            readonly Queue<OptionsData> queue = new Queue<OptionsData>();

            public BitmapFactoryOptionsPool() {}

            public bool TryGetOptions(int maxPoolSize, string filePath, out BitmapFactory.Options options)
            {
                options = null;
                foreach (var item in queue)
                {
                    if (item.FilePath.Equals(filePath)) {
                        options = item.Options;
                        break;
                    }
                }

                if (options != null)
                {
                    return true;
                }
                else
                {
                    if (queue.Count < maxPoolSize)
                    {
                        options = new BitmapFactory.Options();
                        queue.Enqueue(new OptionsData(filePath, options));
                    }
                    else
                    {
                        var item = queue.Dequeue();
                        item.FilePath = filePath;
                        options = item.Options;
                        queue.Enqueue(item);
                    }
                    return false;
                }
            }

            public void ReleaseCache()
            {
                foreach (var item in queue)
                {
                    if (item != null && item.Options != null && item.Options.InBitmap != null)
                    {
                        item.Options.InBitmap.Dispose();
                        item.Options.InBitmap = null;
                    }
                }
                queue.Clear();

                System.GC.Collect();
            }

            class OptionsData
            {
                public string FilePath { get; set; }
                public BitmapFactory.Options Options { get; set; }

                public OptionsData(string filePath, BitmapFactory.Options options)
                {
                    FilePath = filePath;
                    Options = options;
                }
            }
        }

        class BitmapWorkerTask : AsyncTask<object, object, Bitmap>
        {
            const string TAG = "BitmapWorkerTask";

            readonly BitmapLoader parent;
            readonly WeakReference<ImageView> imageViewReference;

            public BitmapWorkerTask(BitmapLoader parent, ImageView imageView)
            {
                this.parent = parent;
                // Use a WeakReference to ensure the ImageView can be garbage collected
                imageViewReference = new WeakReference<ImageView>(imageView);
            }

            // Decode image in background.
            protected override Bitmap RunInBackground(params object[] objArr)
            {
                int maxPoolSize = ((Java.Lang.Integer)objArr[0]).IntValue();
                Context context = (Context)objArr[1];
                string fileName = ((Java.Lang.String)objArr[2]).ToString();
                string assetImageDir = ((Java.Lang.String)objArr[3]).ToString();

                return parent.DecodeBitmap(maxPoolSize, context, fileName, assetImageDir);
            }

            // Once complete, see if ImageView is still around and set bitmap.
            protected override void OnPostExecute(Bitmap bitmap)
            {
                if (imageViewReference != null && bitmap != null)
                {
                    ImageView imageView;
                    if (imageViewReference.TryGetTarget(out imageView))
                    {
                        if (Env.Debug)
                        {
                            Log.Debug(TAG, "Async. Decoding ({0}) done. Bytes:{1}", bitmap.ToString(), bitmap.ByteCount.ToString());
                            TimeProfiler.LogMemInfo();
                        }
                        imageView.SetImageBitmap(bitmap);
                    }
                }
            }
        }
    }
}

