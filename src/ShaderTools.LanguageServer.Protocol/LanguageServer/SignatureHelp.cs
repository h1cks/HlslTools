//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using ShaderTools.LanguageServer.Protocol.MessageProtocol;

namespace ShaderTools.LanguageServer.Protocol.LanguageServer
{
    public class SignatureHelpRequest
    {
        public static readonly
            RequestType<TextDocumentPositionParams, SignatureHelp, object, SignatureHelpRegistrationOptions> Type =
                RequestType<TextDocumentPositionParams, SignatureHelp, object, SignatureHelpRegistrationOptions>.Create("textDocument/signatureHelp");
    }

    public class SignatureHelpRegistrationOptions : TextDocumentRegistrationOptions
    {
        // We duplicate the properties of SignatureHelpOptions class here because
        // we cannot derive from two classes. One way to get around this situation
        // is to use define SignatureHelpOptions as an interface instead of a class.
        public string[] TriggerCharacters { get; set; }
    }

    public class ParameterInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }
    }

    public class SignatureInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }

        public ParameterInformation[] Parameters { get; set; }
    }

    public class SignatureHelp
    {
        public SignatureInformation[] Signatures { get; set; }

        public int? ActiveSignature { get; set; }

        public int? ActiveParameter { get; set; }
    }
}

