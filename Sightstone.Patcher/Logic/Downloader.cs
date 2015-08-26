﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Sightstone.Patcher.Logic
{
    /// <summary>
    /// Downloads multiple files
    /// </summary>
    public class Downloader
    {
        private List<DownloadFile> _downloading;
        private readonly List<DownloadFile> _finished = new List<DownloadFile>();

        public delegate void OnFinished();
        public event OnFinished OnFinishedDownloading;

        public delegate void OnProgressChanged(double downloaded, double toDownload);
        public event OnProgressChanged OnDownloadProgressChanged;

        private double _downloadedBytes;
        private double _bytesToDownload;
        public void DownloadMultipleFiles(List<DownloadFile> filesToDownlad)
        {
            _downloading = filesToDownlad;
            foreach (var fileDlInfo in filesToDownlad)
            {
                foreach (var paths in fileDlInfo.OutputPath.Where(paths => !Directory.Exists(paths)))
                    Directory.CreateDirectory(paths);

                var dlInfo = fileDlInfo;
                foreach (var overwrite in fileDlInfo.OutputPath.Where(overwrite => File.Exists(overwrite) && dlInfo.OverrideFiles))
                    File.Delete(overwrite);

                var req = (HttpWebRequest)WebRequest.Create(fileDlInfo.DownloadUri);
                req.Method = "HEAD";
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    _bytesToDownload = _bytesToDownload + resp.ContentLength;
                }

                using (var client = new WebClient())
                {
                    client.DownloadFileAsync(fileDlInfo.DownloadUri, fileDlInfo.OutputPath[0]);
                    double lastbytes = 0;
                    client.DownloadProgressChanged += (sender, e) =>
                    {
                        Client.RunOnUIThread((() =>
                        {
                            var bytesIn = double.Parse(e.BytesReceived.ToString());
                            var change = bytesIn - lastbytes;
                            _downloadedBytes = _downloadedBytes + change;
                            OnDownloadProgressChanged?.Invoke(_downloadedBytes, _bytesToDownload);
                            lastbytes = bytesIn;
                        }));
                    };
                    var info = fileDlInfo;
                    client.DownloadFileCompleted += (sender, e) =>
                    {
                        _downloading.Remove(info);
                        _finished.Add(info);

                        if (info.OutputPath.Count() != 1)
                        {
                            foreach (var outputs in info.OutputPath.Skip(1))
                            {
                                File.Copy(info.OutputPath[0], outputs, info.OverrideFiles);
                            }
                        }

                        if (_downloading.Count == 0 && _finished.Count != 0)
                        {
                            OnFinishedDownloading?.Invoke();
                        }
                    };
                }
            }
        }
    }

    /// <summary>
    /// Used to tell the downloader what files to download
    /// </summary>
    public class DownloadFile
    {
        /// <summary>
        /// The uri of the file to be downloaded
        /// </summary>
        public Uri DownloadUri { get; set; }

        /// <summary>
        /// The output path of the file
        /// </summary>
        public string[] OutputPath { get; set; }

        /// <summary>
        /// If set to false, the if the file with the same name exists, the file will not be downloaded
        /// </summary>
        public bool OverrideFiles { get; set; }
    }
}