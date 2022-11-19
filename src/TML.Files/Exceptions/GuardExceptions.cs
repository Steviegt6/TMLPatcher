﻿using System;
using System.IO;
using System.Runtime.Serialization;

namespace TML.Files.Exceptions;

[Serializable]
public class TModFileNotFoundException : FileNotFoundException
{
    public TModFileNotFoundException() { }
    public TModFileNotFoundException(string message) : base(message) { }
    public TModFileNotFoundException(string message, Exception inner) : base(message, inner) { }

    protected TModFileNotFoundException(
        SerializationInfo info,
        StreamingContext context
    ) : base(info, context) { }
}

[Serializable]
public class TModFileInvalidHeaderException : IOException
{
    public TModFileInvalidHeaderException() { }
    public TModFileInvalidHeaderException(string message) : base(message) { }
    public TModFileInvalidHeaderException(string message, Exception inner) : base(message, inner) { }

    protected TModFileInvalidHeaderException(
        SerializationInfo info,
        StreamingContext context
    ) : base(info, context) { }
}

[Serializable]
public class TModFileInvalidFileEntryException : IOException
{
    public TModFileInvalidFileEntryException() { }
    public TModFileInvalidFileEntryException(string message) : base(message) { }
    public TModFileInvalidFileEntryException(string message, Exception inner) : base(message, inner) { }

    protected TModFileInvalidFileEntryException(
        SerializationInfo info,
        StreamingContext context
    ) : base(info, context) { }
}

[Serializable]
public class TModFileDirectoryAlreadyExistsException : IOException
{
    public TModFileDirectoryAlreadyExistsException() { }
    public TModFileDirectoryAlreadyExistsException(string message) : base(message) { }
    public TModFileDirectoryAlreadyExistsException(string message, Exception inner) : base(message, inner) { }

    protected TModFileDirectoryAlreadyExistsException(
        SerializationInfo info,
        StreamingContext context
    ) : base(info, context) { }
}

[Serializable]
public class TModFileDirectoryNotFoundException : DirectoryNotFoundException
{
    public TModFileDirectoryNotFoundException() { }
    public TModFileDirectoryNotFoundException(string message) : base(message) { }
    public TModFileDirectoryNotFoundException(string message, Exception inner) : base(message, inner) { }

    protected TModFileDirectoryNotFoundException(
        SerializationInfo info,
        StreamingContext context
    ) : base(info, context) { }
}