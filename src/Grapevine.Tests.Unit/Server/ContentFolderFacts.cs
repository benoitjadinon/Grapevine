﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Grapevine.Server;
using Grapevine.Common;
using Grapevine.Core;
using NSubstitute;
using Shouldly;
using Xunit;
using HttpStatusCode = Grapevine.Common.HttpStatusCode;

namespace Grapevine.Tests.Unit.Server
{
    public class ContentFolderFacts
    {
        private static readonly Random Random = new Random();
        private static string GenerateUniqueString()
        {
            return Guid.NewGuid() + "-" + Random.Next(10, 99);
        }

        public class Constructors
        {
            [Fact]
            public void NoParameters()
            {
                var folder = new ContentFolder();
                folder.IndexFileName.ShouldBe(ContentFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), ContentFolder.DefaultFolderName));
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();
            }

            [Fact]
            public void AbsolutePathShouldNotChange()
            {
                const string path = @"C:\temp";

                var folder = new ContentFolder(path);

                folder.IndexFileName.ShouldBe(ContentFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(path);
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                folder.CleanUp();
            }

            [Fact]
            public void RelativePathShouldBeRelativeToCurrentFolder()
            {
                const string path = "temp";

                var folder = new ContentFolder(path);

                folder.IndexFileName.ShouldBe(ContentFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), path));
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                folder.CleanUp();
            }

            [Fact]
            public void PathAndPrefixSetsPrefix()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                const string prefix = "testing";

                var folder = new ContentFolder(path, prefix);

                folder.IndexFileName.ShouldBe(ContentFolder.DefaultIndexFileName);
                folder.FolderPath.ShouldBe(path);
                folder.Prefix.Equals($"/{prefix}").ShouldBeTrue();
                folder.DirectoryListing.Any().ShouldBeFalse();

                folder.CleanUp();
            }
        }

        public class DefaultFileNameProperty
        {
            [Fact]
            public void DefaultFileNameShowsInDirectoryListingForFileAndFolder()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());

                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, ContentFolder.DefaultIndexFileName), "for testing purposes - delete me");

                var folder = new ContentFolder(path);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(ContentFolder.DefaultIndexFileName)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, ContentFolder.DefaultIndexFileName)).ShouldBe(2);

                folder.CleanUp();
            }

            [Fact]
            public void DefaultFileNameDoesNotChangeWhenSetToEmptyString()
            {
                var folder = new ContentFolder();

                var defaultFileName = folder.IndexFileName;
                folder.IndexFileName = string.Empty;

                folder.IndexFileName.ShouldBe(defaultFileName);
            }

            [Fact]
            public void DefaultFileNameDoesNotChangeWhenSetToNull()
            {
                var folder = new ContentFolder();

                var defaultFileName = folder.IndexFileName;
                folder.IndexFileName = null;

                folder.IndexFileName.ShouldBe(defaultFileName);
            }

            [Fact]
            public void DirectoryListingIsUpdatedWhenDefaultFileNameChanges()
            {
                var defaultFileName1 = ContentFolder.DefaultIndexFileName;
                const string defaultFileName2 = "default.html";

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());

                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, defaultFileName1), "for testing purposes - delete me");
                File.WriteAllText(Path.Combine(path, defaultFileName2), "for testing purposes - delete me");

                var folder = new ContentFolder(path);

                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName1)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, defaultFileName1)).ShouldBe(2);

                folder.IndexFileName = defaultFileName2;

                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key.EndsWith(defaultFileName2)).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(folder.FolderPath, defaultFileName2)).ShouldBe(2);

                folder.CleanUp();
            }
        }

        public class FileSystemWatcherProperty
        {
            [Fact]
            public void CanProvideCustomWatcher()
            {
                var folder = new ContentFolder();
                var watcher = new FileSystemWatcher
                {
                    Path = folder.FolderPath,
                    Filter = "*.jpg",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                };

                folder.Watcher.Filter.ShouldNotBe(watcher.Filter);

                folder.Watcher = watcher;

                folder.Watcher.Filter.ShouldBe(watcher.Filter);
            }

            [Fact]
            public void DoesNotUpdateToNull()
            {
                var folder = new ContentFolder();
                var watcher = folder.Watcher;

                folder.Watcher = null;

                folder.Watcher.ShouldNotBeNull();
                folder.Watcher.Equals(watcher).ShouldBeTrue();
            }

            [Fact]
            public void DoesNotDisposeWhenSetToSameValue()
            {
                var watcher = Substitute.For<FileSystemWatcher>();
                var folder = new ContentFolder { Watcher = watcher };

                folder.Watcher = watcher;

                watcher.DidNotReceive().Dispose();
            }
        }

        public class FolderPathProperty
        {
            [Fact]
            public void CreatesFolderIfNotExists()
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.Exists(path).ShouldBeFalse();

                var folder = new ContentFolder(path);

                Directory.Exists(path).ShouldBeTrue();

                folder.CleanUp();
            }
        }

        public class PrefixProperty
        {
            [Fact]
            public void IsEmptyStringWhenSetToNull()
            {
                var folder = new ContentFolder { Prefix = null };
                folder.Prefix.Equals(string.Empty).ShouldBeTrue();
            }

            [Fact]
            public void PrependsMissingForwardSlash()
            {
                var folder = new ContentFolder { Prefix = "hello" };
                folder.Prefix.Equals("hello").ShouldBeFalse();
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void DoesNotPrependForwadSlashWhenExists()
            {
                var folder = new ContentFolder { Prefix = "/hello" };
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void TrimsTrailingSlash()
            {
                var folder = new ContentFolder { Prefix = "hello/" };
                folder.Prefix.Equals("/hello").ShouldBeTrue();

                folder.Prefix = "/hello/";
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }

            [Fact]
            public void TrimsLeadingAndTrailingWhitespace()
            {
                var folder = new ContentFolder { Prefix = "  /hello/  " };
                folder.Prefix.Equals("/hello").ShouldBeTrue();
            }
        }

        public class CreateDirectoryListKeyMethod
        {
            [Fact]
            public void ReplacesBackslashWithForwardslash()
            {
                var folder = new ContentFolder();
                var path = Path.Combine(folder.FolderPath, "path1", "path2");

                path.Contains(@"\").ShouldBeTrue();
                path.Contains(@"/").ShouldBeFalse();

                path = folder.GetDirectoryListKey(path);

                path.Contains(@"\").ShouldBeFalse();
                path.Contains(@"/").ShouldBeTrue();
            }

            [Fact]
            public void RemovesFolderPath()
            {
                var folder = new ContentFolder();
                var path = Path.Combine(folder.FolderPath, "path1", "path2");

                path.StartsWith(folder.FolderPath).ShouldBeTrue();
                path.ToLower().StartsWith(folder.FolderPath.ToLower()).ShouldBeTrue();

                path = folder.GetDirectoryListKey(path);

                path.Equals("/path1/path2").ShouldBeTrue();
            }

            [Fact]
            public void AppendsPrefix()
            {
                var folder = new ContentFolder { Prefix = "unit-test" };
                var path = folder.GetDirectoryListKey(Path.Combine(folder.FolderPath, "path1", "path2"));

                path.Equals("/unit-test/path1/path2").ShouldBeTrue();
            }
        }

        public class DisposeMethod
        {
            [Fact]
            public void DisposesOfFileSystemWatcher()
            {
                var watcher = Substitute.For<FileSystemWatcher>();
                var folder = new ContentFolder { Watcher = watcher };

                folder.Dispose();

                watcher.Received().Dispose();
            }
        }

        public class FileSystemWatcherEventHandlers
        {
            [Fact]
            public void AddsNewFilesToList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(0);
                folder.Watcher.Created += (sender, args) => { updated.Set(); };

                var filename = GenerateUniqueString();
                var filepath = Path.Combine(path, filename);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key == $"/{filename}" && x.Value == filepath).ShouldBe(1);

                folder.CleanUp();
            }

            [Fact]
            public void RemovesDeletedFilesFromList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.Watcher.Deleted += (sender, args) => { updated.Set(); };

                File.Delete(filepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(0);

                folder.CleanUp();
            }

            [Fact]
            public void ChangesNamesOfRenamedFilesInList()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                var newfilepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(1);

                folder.CleanUp();
            }

            [Fact]
            public void AddsTwoFilesToListWhenAddingDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(0);
                folder.Watcher.Created += (sender, args) => { updated.Set(); };

                var filename = folder.IndexFileName;
                var filepath = Path.Combine(path, filename);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{filename}" && x.Value == filepath).ShouldBe(1);

                folder.CleanUp();
            }

            [Fact]
            public void RemovesTwoFilesFromListWhenRemovingDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, ContentFolder.DefaultIndexFileName);
                File.WriteAllText(filepath, "for testing purposes - delete me");
                File.WriteAllText(Path.Combine(path, GenerateUniqueString()), "for testing purposes - delete me");

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(3);
                folder.DirectoryListing.Count(x => x.Value == Path.Combine(path, ContentFolder.DefaultIndexFileName)).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{ContentFolder.DefaultIndexFileName}").ShouldBe(1);
                folder.Watcher.Deleted += (sender, args) => { updated.Set(); };

                File.Delete(filepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);

                folder.CleanUp();
            }

            [Fact]
            public void UpdatesIndexerWhenChangingToDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, GenerateUniqueString());
                var newfilepath = Path.Combine(path, ContentFolder.DefaultIndexFileName);
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{ContentFolder.DefaultIndexFileName}").ShouldBe(1);

                folder.CleanUp();
            }

            [Fact]
            public void UpdatesIndexerWhenChangingFromDefaultFileName()
            {
                var updated = new ManualResetEvent(false);

                var path = Path.Combine(Directory.GetCurrentDirectory(), GenerateUniqueString());
                Directory.CreateDirectory(path);

                var filepath = Path.Combine(path, ContentFolder.DefaultIndexFileName);
                var newfilepath = Path.Combine(path, GenerateUniqueString());
                File.WriteAllText(filepath, "for testing purposes - delete me");

                var folder = new ContentFolder(path);
                folder.DirectoryListing.Count.ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Value == filepath).ShouldBe(2);
                folder.DirectoryListing.Count(x => x.Key == $"/{ContentFolder.DefaultIndexFileName}").ShouldBe(1);
                folder.Watcher.Renamed += (sender, args) => { updated.Set(); };

                File.Move(filepath, newfilepath);
                updated.WaitOne(1000, false);

                folder.DirectoryListing.Count.ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Value == newfilepath).ShouldBe(1);
                folder.DirectoryListing.Count(x => x.Key == $"/{ContentFolder.DefaultIndexFileName}").ShouldBe(0);

                folder.CleanUp();
            }
        }

        public class SendFileMethod : IDisposable
        {
            private readonly ContentFolder _folder;

            public SendFileMethod()
            {
                _folder = new ContentFolder(GenerateUniqueString());
            }

            public void Dispose()
            {
                _folder.CleanUp();
            }

            [Fact]
            public void CallsSendResponseWhenFileExists()
            {
                //var responded = new ManualResetEvent(false);
                //var filename = GenerateUniqueString();
                //var counter = 0;

                //File.WriteAllText(Path.Combine(_folder.FolderPath, filename), "for testing purposes - delete me");
                //while (_folder.DirectoryListing.Count == 0 && counter < 5)
                //{
                //    Thread.Sleep(100);
                //    counter++;
                //}

                //var context = Substitute.For<HttpContext>();

                //var request = Substitute.For<IInboundHttpRequest<object>>();
                //request.PathInfo.Returns($"/{filename}");

                //var response = Substitute.For<IOutboundHttpResponse<object>>();
                //response.When(x => x.SendResponse(Arg.Any<byte[]>())).Do(info =>
                //{
                //    context.WasRespondedTo.Returns(true);
                //    responded.Set();
                //});

                //context.Request.Returns(request);
                //context.Response.Returns(response);

                //_folder.SendFile(context);
                //responded.WaitOne(300, false);

                //context.Response.Received().SendResponse(Arg.Any<byte[]>());
                //context.WasRespondedTo.ShouldBeTrue();
            }

            [Fact]
            public void DoesNotCallSendResponseWhenFileDoesNotExist()
            {
                //var context = Mocks.HttpContext();
                //context.Request.PathInfo.Returns($"/{GenerateUniqueString()}");
                //context.Response.When(x => x.SendResponse(Arg.Any<byte[]>())).Do(info =>
                //{
                //    context.WasRespondedTo.Returns(true);
                //});

                //_folder.SendFile(context);

                //context.Response.DidNotReceive().SendResponse(Arg.Any<byte[]>());
                //context.WasRespondedTo.ShouldBeFalse();
            }

            [Fact]
            public void ThrowsExceptionWhenFileShouldExist()
            {
                //var prefix = GenerateUniqueString();
                //var context = Mocks.HttpContext();
                //context.Request.PathInfo.Returns($"/{prefix}/{GenerateUniqueString()}");
                //context.Response.When(x => x.SendResponse(Arg.Any<byte[]>())).Do(info =>
                //{
                //    context.WasRespondedTo.Returns(true);
                //});

                //_folder.Prefix = prefix;

                //Should.Throw<Exceptions.Server.FileNotFoundException>(() => _folder.SendFile(context));
            }
        }

        public class SendFileMethodUsingIfModified : IDisposable
        {
            private readonly ContentFolder _folder;

            public SendFileMethodUsingIfModified()
            {
                _folder = new ContentFolder(GenerateUniqueString());
            }

            public void Dispose()
            {
                _folder.CleanUp();
            }

            [Fact]
            public void ReturnsNotModifiedWhenIfModifiedHeaderAndFileHasNotBeenModified()
            {
                //var responded = new ManualResetEvent(false);
                //var filename = GenerateUniqueString();
                //var counter = 0;
                //HttpStatusCode statusCode = 0;

                //var filepath = Path.Combine(_folder.FolderPath, filename);
                //File.WriteAllText(filepath, "for testing purposes - delete me");
                //while (_folder.DirectoryListing.Count == 0 && counter < 5)
                //{
                //    Thread.Sleep(100);
                //    counter++;
                //}

                //var context =
                //    Mocks.HttpContext(new Dictionary<string, object>
                //    {
                //        {
                //            "RequestHeaders",
                //            new WebHeaderCollection
                //            {
                //                {"If-Modified-Since", File.GetLastWriteTimeUtc(filepath).ToString("R")}
                //            }
                //        }
                //    });

                //context.Request.PathInfo.Returns($"/{filename}");
                //context.Response.When(x => x.SendResponse(Arg.Any<byte[]>())).Do(info =>
                //{
                //    statusCode = context.Response.StatusCode;
                //    context.WasRespondedTo.Returns(true);
                //    responded.Set();
                //});

                //_folder.SendFile(context);
                //responded.WaitOne(300, false);

                //context.WasRespondedTo.ShouldBeTrue();
                //statusCode.ShouldBe(HttpStatusCode.NotModified);
            }

            [Fact]
            public void ReturnsFileWhenIfModifiedHeaderAndFileHasBeenModified()
            {
                //var responded = new ManualResetEvent(false);
                //var filename = GenerateUniqueString();
                //var counter = 0;

                //File.WriteAllText(Path.Combine(_folder.FolderPath, filename), "for testing purposes - delete me");
                //while (_folder.DirectoryListing.Count == 0 && counter < 5)
                //{
                //    Thread.Sleep(100);
                //    counter++;
                //}

                //var context = Mocks.HttpContext();
                //context.Request.PathInfo.Returns($"/{filename}");
                //context.Request.Headers.Add("If-Modified-Since", DateTime.Now.AddDays(-1).ToString("R"));
                //context.Response.When(x => x.SendResponse(Arg.Any<byte[]>())).Do(info =>
                //{
                //    context.WasRespondedTo.Returns(true);
                //    responded.Set();
                //});

                //_folder.SendFile(context);
                //responded.WaitOne(300, false);

                //context.WasRespondedTo.ShouldBeTrue();
                //context.Response.StatusCode.ShouldNotBe(HttpStatusCode.NotModified);
            }
        }
    }

    public static class ContentFolderExtensions
    {
        internal static string GetDirectoryListKey(this ContentFolder folder, string item)
        {
            var method = folder.GetType().GetMethod("CreateDirectoryListKey", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = method.Invoke(folder, new object[] { item });
            return (string)result;
        }

        internal static void CleanUp(this ContentFolder folder)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder.FolderPath))
                {
                    File.Delete(file);
                }

                Directory.Delete(folder.FolderPath);
            }
            finally
            {
                folder.Dispose();
            }
        }
    }
}