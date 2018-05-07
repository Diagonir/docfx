// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Docs.Build
{
    public static class BuildTest
    {
        public static readonly TheoryData<string, string> Specs = TestHelper.FindTestSpecs("build");

        [Theory]
        [MemberData(nameof(Specs))]
        public static async Task BuildDocset(string ymlFilePath, string headerName)
        {
            var spec = TestHelper.FindTestSpecInFile(ymlFilePath, headerName);
            Assert.NotNull(spec);

            var docsetPath = spec.CreateDocset();
            try
            {
                await Program.Main(new[] { "build", docsetPath });

                var docsetOutputPath = Path.Combine(docsetPath, "_site");
                var outputs = Directory.EnumerateFiles(docsetOutputPath, "*", SearchOption.AllDirectories);
                Assert.Equal(spec.Outputs.Count, outputs.Count());

                foreach (var (file, content) in spec.Outputs)
                {
                    VerifyFile(Path.GetFullPath(Path.Combine(docsetOutputPath, file)), content);
                }
            }
            catch (Exception e)
            {
                //todo: change the validation way when we have report output
                Assert.NotNull(spec.Exceptions);
                Assert.NotEmpty(spec.Exceptions);
                Assert.True(spec.Exceptions.ContainsKey($"{e.GetType()}"));
                Assert.Equal(spec.Exceptions[$"{e.GetType()}"], e.Message);
            }
        }

        private static void VerifyFile(string file, string content)
        {
            Assert.True(File.Exists(file), $"File should exist: '{file}'");

            switch (Path.GetExtension(file.ToLower()))
            {
                case ".json":
                    TestHelper.VerifyJsonContainEquals(
                        JToken.Parse(content),
                        JToken.Parse(File.ReadAllText(file)));
                    break;

                default:
                    Assert.Equal(
                        content.Trim(),
                        File.ReadAllText(file).Trim(),
                        ignoreCase: false,
                        ignoreLineEndingDifferences: true,
                        ignoreWhiteSpaceDifferences: true);
                    break;
            }
        }
    }
}
