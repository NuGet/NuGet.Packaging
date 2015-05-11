﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.ProjectModel
{
    public sealed class PackageSpecFormatException : Exception
    {
        public PackageSpecFormatException(string message) :
            base(message)
        {
        }

        public PackageSpecFormatException(string message, Exception innerException) :
            base(message, innerException)
        {

        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        private PackageSpecFormatException WithLineInfo(IJsonLineInfo lineInfo)
        {
            Line = lineInfo.LineNumber;
            Column = lineInfo.LinePosition;

            return this;
        }

        public static PackageSpecFormatException Create(Exception exception, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new PackageSpecFormatException(exception.Message, exception)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        public static PackageSpecFormatException Create(string message, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new PackageSpecFormatException(message)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        internal static PackageSpecFormatException Create(JsonReaderException exception, string path)
        {
            return new PackageSpecFormatException(exception.Message, exception)
            {
                Path = path,
                Column = exception.LinePosition,
                Line = exception.LineNumber
            };
        }
    }
}