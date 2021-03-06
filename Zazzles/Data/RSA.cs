﻿/*
 * Zazzles : A cross platform service framework
 * Copyright (C) 2014-2015 FOG Project
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
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Zazzles.Data
{
    public static class RSA
    {
        private const string LogName = "Data::RSA";

        /// <summary>
        ///     Encrypt data using RSA
        /// </summary>
        /// <param name="cert">The X509 certificate to use</param>
        /// <param name="data">The data to encrypt</param>
        /// <returns>A hex string of the encrypted data</returns>
        public static string Encrypt(X509Certificate2 cert, string data)
        {
            if(cert == null)
                throw new ArgumentNullException(nameof(cert));
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Data must be provided", nameof(data));

            var byteData = Encoding.UTF8.GetBytes(data);
            var encrypted = Encrypt(cert, byteData);
            return Transform.ByteArrayToHexString(encrypted);
        }

        /// <summary>
        ///     Decrypt data using RSA
        /// </summary>
        /// <param name="cert">The X509 certificate to use</param>
        /// <param name="data">The data to decrypt</param>
        /// <returns>A UTF8 string of the data</returns>
        public static string Decrypt(X509Certificate2 cert, string data)
        {
            if (cert == null)
                throw new ArgumentNullException(nameof(cert));
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Data must be provided", nameof(data));

            var byteData = Transform.HexStringToByteArray(data);
            var decrypted = Decrypt(cert, byteData);
            return Encoding.UTF8.GetString(decrypted);
        }

        /// <summary>
        ///     Encrypt data using RSA
        /// </summary>
        /// <param name="cert">The X509 certificate to use</param>
        /// <param name="data">The data to encrypt</param>
        /// <returns>A byte array of the encrypted data</returns>
        public static byte[] Encrypt(X509Certificate2 cert, byte[] data)
        {
            if (cert == null)
                throw new ArgumentNullException(nameof(cert));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var rsa = (RSACryptoServiceProvider) cert?.PublicKey.Key;
            return rsa?.Encrypt(data, false);
        }

        /// <summary>
        ///     Decrypt data using RSA
        /// </summary>
        /// <param name="cert">The X509 certificate to use</param>
        /// <param name="data">The data to decrypt</param>
        /// <returns>A byte array of the decrypted data</returns>
        public static byte[] Decrypt(X509Certificate2 cert, byte[] data)
        {
            if (cert == null)
                throw new ArgumentNullException(nameof(cert));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!cert.HasPrivateKey)
                throw new ArgumentException("Certficate must have a private key!", nameof(cert));

            var rsa = (RSACryptoServiceProvider) cert.PrivateKey;
            return rsa.Decrypt(data, false);
        }

        /// <summary>
        ///     Validate that certificate came from a specific CA
        ///     http://stackoverflow.com/a/17225510/4732290
        /// </summary>
        /// <param name="authority">The CA certificate</param>
        /// <param name="certificate">The certificate to validate</param>
        /// <returns>True if the certificate came from the authority</returns>
        public static bool IsFromCA(X509Certificate2 authority, X509Certificate2 certificate)
        {
            if (authority == null)
                throw new ArgumentNullException(nameof(authority));
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            Log.Debug(LogName, "Attempting to verify authenticity of certificate...");
            Log.Debug(LogName, "Authority: " + authority);
            Log.Debug(LogName, "Cert: " + certificate);

            try
            {
                var chain = new X509Chain
                {
                    ChainPolicy =
                    {
                        RevocationMode = X509RevocationMode.NoCheck,
                        RevocationFlag = X509RevocationFlag.ExcludeRoot,
                        VerificationTime = DateTime.Now,
                        UrlRetrievalTimeout = new TimeSpan(0, 0, 0)
                    }
                };

                // This part is very important. You're adding your known root here.
                // It doesn't have to be in the computer store at all. Neither certificates do.
                chain.ChainPolicy.ExtraStore.Add(authority);

                var isChainValid = chain.Build(certificate);

                if (!isChainValid)
                {
                    var errors = chain.ChainStatus
                        .Select(x => $"{x.StatusInformation.Trim()} ({x.Status})")
                        .ToArray();
                    var certificateErrorsString = "Unknown errors.";

                    if (errors.Length > 0)
                        certificateErrorsString = string.Join(", ", errors);

                    Log.Error(LogName, "Certificate validation failed");
                    Log.Error(LogName,
                        "Trust chain did not complete to the known authority anchor. Errors: " + certificateErrorsString);
                    return false;
                }

                // This piece makes sure it actually matches your known root
                if (
                    chain.ChainElements.Cast<X509ChainElement>()
                        .Any(x => x.Certificate.Thumbprint == authority.Thumbprint))
                    return true;

                Log.Error(LogName, "Certificate validation failed");
                Log.Error(LogName,
                    "Trust chain did not complete to the known authority anchor. Thumbprints did not match.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Could not verify certificate is from CA");
                Log.Error(LogName, ex);
                return false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>The certificate used to digitally sign a file</returns>
        public static X509Certificate2 ExtractDigitalSignature(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path must be provided!", nameof(filePath));

            try
            {
                var signer = X509Certificate.CreateFromSignedFile(filePath);
                var certificate = new X509Certificate2(signer);
                return certificate;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>The FOG CA root certificate</returns>
        public static X509Certificate2 ServerCertificate()
        {
            return GetRootCertificate("FOG Server CA");
        }

        /// <summary>
        /// </summary>
        /// <returns>The FOG Project root certificate</returns>
        public static X509Certificate2 FOGProjectCertificate()
        {
            return GetRootCertificate("FOG Project");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of the certificate to retrieve</param>
        /// <returns>Returns the first instance of the certificate matching the name</returns>
        public static X509Certificate2 GetRootCertificate(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Certificate name must be provided!", nameof(name));

            try
            {
                X509Certificate2 CAroot = null;
                var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var cers = store.Certificates.Find(X509FindType.FindBySubjectName, name, true);

                if (cers.Count > 0)
                {
                    Log.Entry(LogName, name + " cert found");
                    CAroot = cers[0];
                }
                store.Close();

                return CAroot;
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Unable to retrieve " + name);
                Log.Error(LogName, ex);
            }

            return null;
        }

        /// <summary>
        ///     Add a CA certificate to the machine store
        /// </summary>
        /// <param name="caCert">The certificate to add</param>
        public static bool InjectCA(X509Certificate2 caCert)
        {
            if (caCert == null)
                throw new ArgumentNullException(nameof(caCert));

            Log.Entry(LogName, "Injecting root CA: " + caCert.FriendlyName);
            try
            {
                var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(caCert);
                store.Close();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Unable to inject CA");
                Log.Error(LogName, ex);
            }

            return false;
        }
    }
}