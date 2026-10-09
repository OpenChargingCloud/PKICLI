# PKICLI

[![CI](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/ci.yml/badge.svg)](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/ci.yml)
[![Nightly](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/nightly.yml/badge.svg)](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/nightly.yml)

One public key infrastructure, with a web interface and a prompt, until
'quit' or Ctrl+C.

The PKI makes the X.509 certificates every other program of this family is
known by: root CAs, sub-CAs below them, certificates for servers - a CSMS, a
local controller - and for clients - a charging station towards its CSMS -
and certificates for the key of a certificate signing request, such as a
charging station sends when it wants a new OCPP client certificate.

It is a sibling of [EVCLI](https://github.com/OpenChargingCloud/EVCLI),
[GatewayCLI](https://github.com/OpenChargingCloud/GatewayCLI) and the others,
and is built the same way: a [Hermod](https://github.com/Vanaheimr/Hermod)
HTTP server carrying a JSON API and one Server-Sent Events stream, and a web
interface built by npm and embedded into the assembly. What every program of
the family has - the sign-in, the name servers (DNS), the time servers (NTS),
the certificates they are held to, the SSH server and the log - is
[WWCP_Node](https://github.com/OpenChargingCloud/WWCP_Node); what is the PKI's
own is in [PKI](https://github.com/OpenChargingCloud/PKI).


### Getting it

The libraries it is built from are submodules, so they have to come along:

```
git clone --recurse-submodules https://github.com/OpenChargingCloud/PKICLI.git
```

If you already cloned it without them:

```
git submodule update --init --recursive
```

**On Windows**, turn long paths on first: `git config --global core.longpaths true`.


### Building and running it

.NET 10 and Node 22 or newer. The build runs `npm ci` and `npm run build` for
the web interface itself whenever its inputs changed:

```
dotnet build PKICLI.slnx
./run.sh
```

It listens on http://127.0.0.1:2360/ - `--any` for every address, `--port`
for another one, `--help` for every switch. At the first start it makes one
account, `root`, and shows its password once, on the console.

Beside the solution it writes `configuration.json`, `accounts/`, `logs/`,
`certificates/` - what the PKI itself believes, the roots of its time servers
say - and **`pki/`**, what it makes: one file per certificate and one per
private key, named by the SHA-256 fingerprint, and `index.json` beside them.
The private keys are kept **unencrypted**; whoever can read `pki/` can sign as
every CA in it. All of these are in `.gitignore`.


### The web interface

| | |
|---|---|
| **PKI** (`/pki`) | every certificate of the PKI, filtered by a text in its common name and by its profile; the details of one - subject, chain, validity, key, fingerprint - with its PEM to copy or download, its chain, the private key of a server's or client's certificate made here, and Delete; and the forms: **New root CA**, **New sub-CA**, **New certificate** (server or client, with a key made here) and **Sign a CSR** |
| **Configuration** | what the PKI is running, how many certificates of each profile it keeps, and below it the **DNS client**, the **NTS client**, the **SSH server**, the **Trusted certificates** and **Identities** of the node itself |
| **Logs** | the log as it happens |

Deleting is hard and final: the files and the key are gone. A CA that signed
certificates still in the PKI is deleted only together with everything below
it, and only after a question that says how many. Deleting is not revoking -
whoever believes the root of a certificate that went out goes on believing it.


### The JSON API

What every node answers is WWCP_Node's. The PKI adds, below `/api/v1/pki`:

| | |
|---|---|
| `GET certificates?commonName=&profile=` | every certificate, or those whose common name holds a text and of a profile (`rootCA`, `subCA`, `server`, `client`) |
| `GET certificates/{id}` | one, with its PEM, its chain without the root, and how many it signed |
| `GET certificates/{id}/key` | the private key of a server's or client's certificate made here - never a CA's |
| `DELETE certificates/{id}?withIssued=true` | delete it, with everything it signed |
| `POST rootCAs`, `POST subCAs` | `{ subject, keyAlgorithm, validDays, pathLength }`, a sub-CA with its `issuer` |
| `POST certificates` | `{ issuer, profile, subject, keyAlgorithm, validDays, subjectAlternativeNames }` |
| `POST csr`, `POST csr/inspect` | `{ issuer, profile, csr, validDays, subjectAlternativeNames, takeSubjectAlternativeNames }`, and what a CSR says before that |

A `subject` is `{ commonName, organization, organizationalUnit, country,
state, locality }`, of which only the common name is needed. Key algorithms are
`ecc-p256`, `ecc-p384`, `ecc-p521`, `rsa-2048`, `rsa-3072` and `rsa-4096`.

A CSR is signed for its key and as what was chosen: its subject is taken over,
its alternative names where that is asked for, and nothing else it asks for -
a CSR asking to be a CA is still signed as a client's certificate. A program
that sends CSRs on its own - a backend for its charging stations - signs in
with an API key of an account in the `registrar` role.


### Who may do what

The node's `viewer` may look at everything and `systemadmin` may do
everything. A PKI adds two resources, `authorities` - making and deleting CAs -
and `issuance` - signing certificates for servers and clients, downloading
their keys and deleting them - and two roles: `operator`, who looks at
everything and asks whether the network works, and `registrar`, who signs
certificates below the CAs that are there. The `roles` section of
`configuration.json` may add more, as on every node.


### What is not here yet

Revocation lists and OCSP, certificate transparency logs, a PKCS#12 download,
a password on the private keys, and an automatic CSR endpoint for charging
stations of its own (EST, or OCPP's `SignCertificate` through a CSMS).


## Your participation

This software is Open Source under the **Affero GPL 3.0 license**.
We appreciate your participation in this ongoing project, and your help to
improve it and the e-mobility ICT in general. If you find bugs, want to
request a feature or send us a pull request, feel free to use the normal
GitHub features to do so. For this please read the Contributor License
Agreement carefully and send us a signed copy or use a similar free and
open license.
