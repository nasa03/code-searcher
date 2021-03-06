﻿using CodeSearcher.BusinessLogic.Common;
using CodeSearcher.BusinessLogic.InternalInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CodeSearcher.BusinessLogic.Io
{
    internal class FileReader : IFileReader
    {
		private HashSet<String> m_FileExtensions;

        public  FileReader(IList<String> fileExtensions)
        {
			m_FileExtensions = new HashSet<String>(fileExtensions);
        }

        public Task ReadFilesAsync(String srcPath, Action<IList<FileStructure>> action)
        {
            return ReadFilesAsync(srcPath, action, 0);
        }

        private Task ReadFilesAsync(String srcPath, Action<IList<FileStructure>> action, int depth)
        {
            return Task.Run(() =>
            {
                if (depth >= 3)
                {
                    ReadFilesInSync(srcPath, action);
                }
				else
                {
                    var currentDirectory = new DirectoryInfo(srcPath);
                    if (!currentDirectory.Exists) throw new ArgumentException("Source Folder dosn't exist!");

                    var data = ReadFolderContent(currentDirectory);
                    action(data);

                    var tasks = new List<Task>();
                    foreach (var innerDirectory in currentDirectory.EnumerateDirectories())
                    {
                        var innerTask = ReadFilesAsync(innerDirectory.FullName, action, ++depth);
                        tasks.Add(innerTask);
                    }

                    Task.WaitAll(tasks.ToArray());
                }
            });
        }

        public void ReadFilesInSync(String srcPath, Action<IList<FileStructure>> action)
        {
            var currentDirectory = new DirectoryInfo(srcPath);
            if (!currentDirectory.Exists) throw new ArgumentException("Source Folder dosn't exist!");

            var data = ReadFolderContent(currentDirectory);
            action(data);

            foreach (var innerDirectory in currentDirectory.EnumerateDirectories())
            {
                ReadFilesInSync(innerDirectory.FullName, action);

            }      
        }

        private IList<FileStructure> ReadFolderContent(DirectoryInfo di)
        {
            var files = new List<FileStructure>();
            var resultTexts = new List<FileStructure>();

            if (string.Compare(di.Name, FolderNames.DefaultLuceneIndexName, StringComparison.OrdinalIgnoreCase) != 0)
            {
                foreach (var file in di.GetFiles())
                {
                    if (m_FileExtensions.Contains(file.Extension))
                    {
                        files.Add(ReadText(file.FullName));
                    }
                }

                foreach (var file in files)
                {
                    resultTexts.Add(file);
                }
            }

            return resultTexts;
        }

        private FileStructure ReadText(string filePath)
        {
            try
            {
                using (FileStream sourceStream = new FileStream(filePath,
                    FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 4096, useAsync: false))
                {
                    var sb = new StringBuilder();

                    byte[] buffer = new byte[0x1000];
                    int numRead;
                    while ((numRead = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        string text = Encoding.Default.GetString(buffer, 0, numRead);
                        sb.Append(text);
                    }

                    return new FileStructure
                    {
                        FilePath = filePath,
                        Text = sb.ToString(),
                        ErrorOccurred = false
                    };
                }
            }
            catch (Exception e)
            {
                var exp = e;
                while (exp != null)
                {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                    Console.Error.WriteLine("~~~~~~~~~~~~~~~");
                    exp = exp.InnerException;
                }

                return new FileStructure
                {
                    FilePath = filePath,
                    Text = string.Empty,
                    ErrorOccurred = true,
                };
            }
        }
    }
}
