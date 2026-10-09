/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of PKICLI <https://github.com/OpenChargingCloud/PKICLI>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using cloud.charging.open.protocols.WWCP.Node.CommandLine;
using cloud.charging.open.protocols.WWCP.Node.Configuration;

using cloud.charging.open.PKI.CommandLine;

#endregion

namespace cloud.charging.open.PKI
{

    /// <summary>
    /// One PKI, with its web interface and a prompt, until 'quit', Ctrl+C
    /// or SIGTERM.
    /// </summary>
    /// <remarks>
    /// What every kind of node's program does is the node's: the switches and
    /// the words -h explains them with, why it could not be set up or could not
    /// start, what goes into the certificate store, the banner and the prompt.
    /// A PKI adds no switch and no line of its own to any of them - its
    /// certificates live beside the configuration file - so what is left here
    /// is which program this is, and the PKI it makes.
    /// </remarks>
    public class Program
    {

        #region (private static) Usage

        /// <summary>
        /// What -h shows: every node's switches, in a PKI's words.
        /// </summary>
        /// <remarks>
        /// What --config is explained with is the node's own sentence, which
        /// is the PKI's word for word.
        /// </remarks>
        private static readonly NodeUsage Usage = new (

            Program:           "PKICLI",
            Kind:              PKI.PKIKind,
            DefaultPort:       PKI.DefaultHTTPPort,
            FrontendSources:   "libs/PKI/PKI/Frontend",
            CertificateKinds:  PKI.CertificateKinds

        );

        #endregion


        public static async Task<Int32> Main(String[] Arguments)
        {

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            #region Arguments

            // Every node's switches; a PKI has none of its own.
            var arguments = NodeArguments.Parse(Arguments);

            if (arguments.Refused(Usage) is Int32 refused)
                return refused;

            if (arguments.RefuseTheRest(Usage) is Int32 unknown)
                return unknown;

            var root = NodeProgram.RepositoryRoot("PKICLI.slnx");

            #endregion

            #region The PKI

            PKI pki;

            try
            {
                pki = new PKI(
                              HTTPHostname:      arguments.HTTPHostname,
                              HTTPPort:          arguments.Port,
                              AccountsPath:      arguments.AccountsPathBelow(root),
                              ConfigFile:        new WWCPConfigFile(arguments.ConfigFilePathBelow(root)),
                              Frontend:          arguments.Frontend,
                              CertificatesPath:  arguments.CertificatesPath,
                              ConsoleLogLevel:   arguments.ConsoleLogLevel,
                              LogPath:           arguments.LogPathBelow(root),
                              BridgeDebugLog:    !arguments.NoTrace,
                              SSH:               arguments.SSH
                          );
            }
            catch (Exception e)
            {
                return NodeProgram.CouldNotBeSetUp(PKI.PKIKind, e, arguments.Verbose);
            }

            await using (pki)
            {

                // What somebody signed in over SSH gets: this program's own command
                // line, with its commands beside the node's.
                pki.CommandLines = (terminal, caller) => new PKICLI(pki, terminal, caller);

                if (pki.ImportCertificates(arguments, out _) is Int32 notImported)
                    return notImported;

                if (arguments.ListCertificates)
                    pki.ListCertificates();

                if (await pki.Started(arguments.Verbose) is Int32 notStarted)
                    return notStarted;

                #region What somebody who just started this needs to know

                foreach (var line in pki.Banner())
                    Console.WriteLine(line);

                #endregion

                #region The command line, until 'quit', Ctrl+C or SIGTERM

                // The node's: a prompt where somebody can type, and waiting
                // where nobody can, with the log sharing the screen.
                await new PKICLI(pki).RunUntilStopped();

                #endregion

            }

            #endregion

            return 0;

        }

    }

}
