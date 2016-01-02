﻿/*
 * Zazzles : A cross platform service framework
 * Copyright (C) 2014-2016 FOG Project
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 3
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using Zazzles.Middleware.Bindings;

namespace Zazzles.Middleware
{
    public static class Communication
    {
        private const string LogName = "Middleware::Communication";
        private static IServerBinding _binding;

        /// <summary>
        /// Bind server communication to RemoteRX and RemoteTX Bus channels
        /// </summary>
        /// <returns>True on success</returns>
        public static bool BindServerToBus()
        {
            _binding = new SocketIO();
            if (_binding.Bind()) return true;

            _binding = new Polling();
            return _binding.Bind();
        }

        /// <summary>
        /// UnBind server communication to RemoteRX and RemoteTX Bus channels
        /// </summary>
        /// <returns>True on success</returns>
        public static bool UnBindServerFromBus()
        {
            return _binding.UnBind();
        }

        public static JObject Get(string url, JObject data)
        {
            return _binding.Get(url, data);
        }

        public static JObject Post(string url, JObject data)
        {
            return _binding.Post(url, data);
        }

        /// <summary>
        ///     Get the text response of a url
        /// </summary>
        /// <param name="postfix">The postfix to attach to the server address</param>
        /// <returns>The unparsed response</returns>
        public static string GetText(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("A URL must be provided!", nameof(url));

            Log.Entry(LogName, "URL: " + url);

            var webRequest = WebRequest.Create(url);

            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                var result = reader.ReadToEnd();
                return result;
            }
        }


        /// <summary>
        ///     Download a file from a server
        /// </summary>
        /// <param name="url">The URL to download from</param>
        /// <param name="filePath">The path to save the file to</param>
        /// <returns>True if successful</returns>
        public static bool DownloadFile(string url, string filePath)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("A URL must be provided!", nameof(url));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("A file path must be provided!", nameof(filePath));

            Log.Entry(LogName, $"URL: {url}");

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                }

                using (var client = new WebClient())
                {
                    client.DownloadFile(url, filePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Could not download file");
                Log.Error(LogName, ex);
                return false;
            }

            return File.Exists(filePath);
        }
    }
}