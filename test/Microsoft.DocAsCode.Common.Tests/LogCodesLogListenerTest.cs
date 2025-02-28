﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Common.Tests
{
    using Microsoft.DocAsCode.Common;

    using Xunit;

        [Collection("docfx STA")]
    public class LogCodesLogListenerTest
    {
        [Fact]
        public void TestLogCodes()
        {
            var logCodesLogListener = new LogCodesLogListener();
            Logger.RegisterListener(logCodesLogListener);
            Logger.LogWarning("message1", file: "file.md", code: WarningCodes.Build.InvalidFileLink);
            Logger.LogWarning("message2", file: "file.md", code: WarningCodes.Build.InvalidBookmark);
            Logger.LogWarning("message3", file: "anotherFile.md", code: WarningCodes.Build.InvalidFileLink);


            Assert.True(logCodesLogListener.Codes.TryGetValue("file.md", out var fileCodes));
            Assert.Contains(WarningCodes.Build.InvalidFileLink, fileCodes);
            Assert.Contains(WarningCodes.Build.InvalidBookmark, fileCodes);
            Assert.True(logCodesLogListener.Codes.TryGetValue("anotherFile.md", out var anotherFileCodes));
            Assert.Contains(WarningCodes.Build.InvalidFileLink, anotherFileCodes);

            Logger.UnregisterAllListeners();
        }
    }
}
