﻿using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApplication2.Sources.Network;
using WindowsFormsApplication2.Sources.Franpette;
using System.ComponentModel;
using System.Collections.Generic;

namespace WindowsFormsApplication2.Sources
{
    class NetworkFTP : IFranpetteNetwork
    {
        private Stopwatch   _sw;
        private Label       _progress;
        private Font        _font;
        private PointF      _textPos;
        
        private String      _password;
        private String      _login;
        private String      _address;

        public NetworkFTP(Label progress_label)
        {
            _sw = new Stopwatch();
            _progress = progress_label;
            _font = new Font("Lucida Sans Unicode", 9F, FontStyle.Regular);
            _textPos = new PointF(0, 0);
        }

        public Boolean connect(string address)
        {
            _address = address;
            return true;
        }

        public Boolean login(string login, string password)
        {
            _login = login;
            _password = password;
            return true;
        }

        // Actions depuis le serveur
        public virtual Boolean downloadFile(ETarget target, BackgroundWorker worker)
        {
            switch (target)
            {
                case ETarget.UPDATE:
                    downloadDirectory("Franpette/Update/", Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
                    break;
                case ETarget.FRANPETTE:
                    ftpDownload("Franpette/FranpetteStatus.xml", FranpetteUtils.getRoot("FranpetteStatus.xml"), worker);
                    printInfo("Franpette est prêt.");
                    break;
                case ETarget.MINECRAFT:
                    printInfo("Franpette inspècte vos fichiers...");
                    File.WriteAllLines(FranpetteUtils.getRoot("Minecraft.csv"), FranpetteUtils.checkCsv(true, FranpetteUtils.getRoot("Minecraft")));
                    ftpDownload("Franpette/Minecraft.csv", FranpetteUtils.getRoot("Minecraft_server.csv"), worker);
                    filesToDownload(FranpetteUtils.getRoot("Minecraft_server.csv"), FranpetteUtils.getRoot("Minecraft.csv"), worker);
                    printInfo("Franpette inspècte vos fichiers...");
                    File.WriteAllLines(FranpetteUtils.getRoot("Minecraft.csv"), FranpetteUtils.checkCsv(true, FranpetteUtils.getRoot("Minecraft")));
                    ftpUpload(FranpetteUtils.getRoot("Minecraft.csv"), "Franpette/Minecraft.csv", worker);
                    break;
                default:
                    FranpetteUtils.debug("[NetworkFTP] downloadFile : Target is missing.");
                    break;
            }
            return true;
        }

        // Actions vers le serveur
        public virtual Boolean uploadFile(ETarget target, BackgroundWorker worker)
        {
            switch (target)
            {
                case ETarget.FRANPETTE:
                    ftpUpload(FranpetteUtils.getRoot("FranpetteStatus.xml"), "Franpette/FranpetteStatus.xml", worker);
                    break;
                case ETarget.MINECRAFT:
                    printInfo("Franpette inspècte vos fichiers...");
                    File.WriteAllLines(FranpetteUtils.getRoot("Minecraft.csv"), FranpetteUtils.checkCsv(true, FranpetteUtils.getRoot("Minecraft")));
                    ftpDownload("Franpette/Minecraft.csv", FranpetteUtils.getRoot("server.csv"), worker);
                    filesToUpload(FranpetteUtils.getRoot("Minecraft.csv"), FranpetteUtils.getRoot("server.csv"), worker);
                    ftpUpload(FranpetteUtils.getRoot("Minecraft.csv"), "Franpette/Minecraft.csv", worker);
                    break;
                default:
                    FranpetteUtils.debug("[NetworkFTP] uploadFile : Target is missing.");
                    break;
            }
            return true;
        }

        // Connexion à un Path sur le server FTP avec les identifiants
        private FtpWebRequest requestMethod(string path, string method)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + _address + "/" + path);
            request.Credentials = new NetworkCredential(_login, _password);
            request.Method = method;
            return request;
        }

        // Récupération de la taille du fichier téléchargé pour la progressBar
        private int requestSize(string path, FtpWebRequest request)
        {
            WebRequest sizeRequest = WebRequest.Create("ftp://" + _address + "/" + path);
            sizeRequest.Credentials = request.Credentials;
            sizeRequest.Method = WebRequestMethods.Ftp.GetFileSize;
            return (int)sizeRequest.GetResponse().ContentLength;
        }

        // Upload
        private void ftpUpload(string src, string dest, BackgroundWorker worker)
        {
            FtpWebRequest request = requestMethod(dest, WebRequestMethods.Ftp.UploadFile);

            _sw.Reset();
            try
            {
                using (Stream fileStream = File.OpenRead(src))
                using (Stream ftpStream = request.GetRequestStream())
                {
                    byte[] buffer = new byte[10240];
                    int read;
                    _sw.Start();
                    FranpetteUtils.debug("[NetworkFTP] ftpUpload : ...uploading " + src);
                    while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ftpStream.Write(buffer, 0, read);
                        printProgressInfo(src, fileStream.Position, (int)fileStream.Length);
                    }
                    _sw.Stop();
                    FranpetteUtils.debug("[NetworkFTP] ftpUpload : " + src + " uploaded !");
                }
            }
            catch (WebException e)
            {
                FranpetteUtils.debug("[NetworkFTP] ftpDownload : " + e.Message);
            }
        }

        // Download
        private void ftpDownload(string src, string dest, BackgroundWorker worker)
        {
            FtpWebRequest request = requestMethod(src, WebRequestMethods.Ftp.DownloadFile);

            int total = requestSize(src, request);
            _sw.Reset();
            try
            {
                using (Stream ftpStream = request.GetResponse().GetResponseStream())
                using (Stream fileStream = File.Create(dest))
                {
                    byte[] buffer = new byte[102400];
                    int read;
                    _sw.Start();
                    FranpetteUtils.debug("[NetworkFTP] ftpDownload : ...downloading " + dest);
                    while ((read = ftpStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, read);
                        printProgressInfo(dest, fileStream.Position, total);
                    }
                    _sw.Stop();
                    FranpetteUtils.debug("[NetworkFTP] ftpDownload : " + dest + " downloaded !");
                }
            }
            catch (WebException e)
            {
                FranpetteUtils.debug("[NetworkFTP] ftpDownload : " + e.Message);
            }
        }

        // Afficher les infos d'upload ou de download sur la progressBar
        public void printProgressInfo(string filename, long pos, int max)
        {
            string perSeconds = "";
            if (max > 102400)
            {
                float speed = pos / (float)_sw.Elapsed.TotalSeconds;
                perSeconds = " - " + (speed / 1000).ToString("F") + " Kb/s";
                if (speed > 1000000)
                    perSeconds = " - " + (speed / 1000000).ToString("F") + " Mb/s";
            }

            float percent = ((float)pos / max) * 100f;
            string percentage = ((int)percent).ToString() + "%";

            printInfo(Path.GetFileName(filename) + perSeconds + " - " + percentage);
        }

        // Afficher au dessus de la barre de progression un message
        public void printInfo(string info)
        {
            _progress.CreateGraphics().Clear(Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(100)))), ((int)(((byte)(130))))));
            _progress.CreateGraphics().DrawString(info, _font, Brushes.White, _textPos);
        }

        // Téléchargement des fichiers
        public void filesToDownload(string server, string local, BackgroundWorker worker)
        {
            string[] localFiles = File.ReadAllLines(local);
            string[] serverFiles = File.ReadAllLines(server);

            int done = 0;
            Boolean found;
            int i = 0;
            int start = 0;

            foreach (string serv in serverFiles)
            {
                string servPath = serv.Split(';')[0];

                // If minecraft_server.jar is found -> create appropriate start.bat
                if ((servPath.Contains("minecraft") || servPath.Contains("server")) && servPath.Contains(".jar"))
                {
                    string[] scriptLines = new string[3];
                    scriptLines[0] = "cd " + FranpetteUtils.getRoot(Path.GetDirectoryName(servPath));
                    scriptLines[1] = "cls";
                    scriptLines[2] = "java -Xms1024M -Xmx2048M -jar " + Path.GetFileName(servPath) + " nogui";
                    File.WriteAllLines(FranpetteUtils.getRoot("start.bat"), scriptLines);
                }

                string file = servPath.Replace('\\', '/');

                found = false;
                i = start;
                while (i < localFiles.Length)
                {
                    if (serv == localFiles[i])
                    {
                        start++;
                        found = true;
                        break;
                    }
                    else if (serv.Split(';')[0] == localFiles[i].Split(';')[0])
                    {
                        start++;
                        found = true;
                        Directory.CreateDirectory(FranpetteUtils.getRoot(file.Substring(0, file.LastIndexOf('/'))));
                        if (serv.Split(';')[1] != localFiles[i].Split(';')[1])
                        {
                            ftpDownload("Franpette/" + file, FranpetteUtils.getRoot(file), worker);
                        }
                        break;
                    }
                    i++;
                }
                if (!found)
                {
                    Directory.CreateDirectory(FranpetteUtils.getRoot(file.Substring(0, file.LastIndexOf('/'))));
                    ftpDownload("Franpette/" + file, FranpetteUtils.getRoot(file), worker);
                }
                worker.ReportProgress((int)(done * 100.0 / (float)serverFiles.Length));
                if (done + 1 <= serverFiles.Length) done++;
            }
        }

        // Upload des fichiers
        public void filesToUpload(string local, string server, BackgroundWorker worker)
        {
            string[] localFiles = File.ReadAllLines(server);
            string[] serverFiles = File.ReadAllLines(local);

            int done = 0;
            Boolean found;
            int i = 0;
            int start = 0;

            foreach (string serv in serverFiles)
            {
                string file = serv.Split(';')[0].Replace('\\', '/');

                found = false;
                i = start;
                while (i < localFiles.Length)
                {
                    if (serv == localFiles[i])
                    {
                        start++;
                        found = true;
                        break;
                    }
                    else if (serv.Split(';')[0] == localFiles[i].Split(';')[0])
                    {
                        start++;
                        found = true;
                        if (serv.Split(';')[1] != localFiles[i].Split(';')[1])
                        {
                            ftpUpload(FranpetteUtils.getRoot(file), "Franpette/" + file, worker);
                        }
                        break;
                    }
                    i++;
                }
                if (!found) ftpUpload(FranpetteUtils.getRoot(file), "Franpette/" + file, worker);
                worker.ReportProgress((int)(done * 100.0 / (float)localFiles.Length));
                if (done + 1 <= localFiles.Length) done++;
            }
        }

        // Télécharge le contenu du dossier distant et de ses sous dossiers
        public void downloadDirectory(string url, string localPath)
        {
            FtpWebRequest listRequest = requestMethod(url, WebRequestMethods.Ftp.ListDirectoryDetails);

            List<string> lines = new List<string>();

            using (FtpWebResponse listResponse = (FtpWebResponse)listRequest.GetResponse())
            using (Stream listStream = listResponse.GetResponseStream())
            using (StreamReader listReader = new StreamReader(listStream))
            {
                while (!listReader.EndOfStream)
                {
                    lines.Add(listReader.ReadLine());
                }
            }

            Directory.CreateDirectory(localPath);

            foreach (string line in lines)
            {
                string[] tokens = line.Split(new[] { ' ' }, 9, StringSplitOptions.RemoveEmptyEntries);
                string name = tokens[8];
                string permissions = tokens[0];

                string localFilePath = Path.Combine(localPath, name);
                string fileUrl = url + name;

                if (permissions[0] == 'd')
                {
                    Directory.CreateDirectory(localFilePath);
                    downloadDirectory(fileUrl + "/", localFilePath);
                }
                else
                {
                    FtpWebRequest downloadRequest = requestMethod(fileUrl, WebRequestMethods.Ftp.DownloadFile);

                    using (FtpWebResponse downloadResponse = (FtpWebResponse)downloadRequest.GetResponse())
                    using (Stream sourceStream = downloadResponse.GetResponseStream())
                    using (Stream targetStream = File.Create(localFilePath))
                    {
                        byte[] buffer = new byte[10240];
                        int read;
                        while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            targetStream.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }
    }
}
