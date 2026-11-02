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

**`--any` puts the sign-in on the network** - the password, the API keys and
the session cookie, and with them the private keys `GET
/v1/pki/certificates/{id}/key` hands out - in the clear, unless the PKI speaks
HTTPS itself or a proxy in front of it does and is named as trusted. Either
is said by switches, `--https`, `--https-cert <file>`, `--https-key <file>`,
`--redirect-port <number>` and `--trusted-proxy <address>`, or in the `web`
section of the configuration file. Behind nginx:

```json
"web": { "trustedProxies": [ "127.0.0.1", "::1" ] }
```

With PEM files of its own (a certificate this PKI issued itself will do) and
no proxy:

```json
"web": { "tls": { "certificate": { "fullchain":  "/etc/pki/pki.example.org/fullchain.pem",
                                   "privateKey": "/etc/pki/pki.example.org/privkey.pem" },
                  "redirectPort": 80 } }
```

Neither said, the PKI warns in its log, below its banner and above every page
of its web interface to its administrators.
[WWCP_Node's README](https://github.com/OpenChargingCloud/WWCP_Node) says the
rest.

Beside the solution it writes `configuration.json`, `accounts/`, `logs/`,
`certificates/` - what the PKI itself believes, the roots of its time servers
say - and **`pki/`**, what it makes: one file per certificate and one per
private key, named by the SHA-256 fingerprint, `index.json` beside them, and
`pki/transparency/`, its certificate transparency log. All of these are in
`.gitignore`.

**A private key may have a password, and need not.** Without one it is kept
unencrypted, and whoever can read `pki/` can sign as every CA whose key has
none - the log says how many there are at every start. With one, it is an
`ENCRYPTED PRIVATE KEY` - PKCS#8, AES-256-CBC, its key derived from the
password by PBKDF2 with SHA-256 and 600,000 iterations - which openssl opens as
it opens any other. The password is kept nowhere: a CA whose key has one signs
only where it is given again, and the key of a server's or a client's
certificate is downloaded encrypted, for whoever has it to open with the
password. A password has 8 characters at least; one that is forgotten makes
its key useless. It is given where a key is made, and given, changed or taken
away later in the details of its certificate.

**Every certificate the PKI signs is in its certificate transparency log**,
after RFC 6962: in the order it was signed, in a Merkle tree whose head the log
signs with a key of its own, an ECDSA key of P-256 in
`pki/transparency/log-key.pem`. A certificate is written to the log before it
is kept anywhere else, so there is none the log does not have, and it stays in
the log when it is deleted from the PKI - that is the point of one. Whoever
keeps a signed tree head can have the log prove that a certificate is in it,
and that the log is the one they saw before, grown and nothing else. A PKI from
before its log has every certificate it keeps taken in at the first start, CAs
first.

`entries.jsonl` is only ever appended to, and `sth.json` keeps the newest tree
head. At every start the log checks every signature in it and its tree against
that head; where they do not hold - an entry changed or taken out - the log is
**broken**, signs no tree head, and the PKI signs nothing until it is repaired,
rather than sign what the log does not have. Half an entry at its end, written
when the PKI stopped, is cut off.


### The web interface

| | |
|---|---|
| **PKI** (`/pki`) | every certificate of the PKI, filtered by a text in its common name and by its profile; the details of one - subject, chain, validity, key, fingerprint - with its PEM to copy or download, its chain, the private key of a server's or client's certificate made here, the password of its key, and Delete; and the forms: **New root CA**, **New sub-CA**, **New certificate** (server or client, with a key made here) and **Sign a CSR**; and the **Transparency log**: its address for a monitor, its id, its newest tree head and public key, and every entry, the newest first, a deleted certificate among them |
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
| `GET certificates/{id}/key` | the private key of a server's or client's certificate made here, as it is kept - encrypted where it has a password, and `encrypted` says so - never a CA's |
| `POST certificates/{id}/key/password` | `{ password, newPassword }`: give its key a password, change it, or take it away with `null` |
| `DELETE certificates/{id}?withIssued=true` | delete it, with everything it signed |
| `POST rootCAs`, `POST subCAs` | `{ subject, keyAlgorithm, validDays, pathLength, keyPassword }`, a sub-CA with its `issuer` and `issuerKeyPassword` |
| `POST certificates` | `{ issuer, issuerKeyPassword, profile, subject, keyAlgorithm, validDays, subjectAlternativeNames, keyPassword }` |
| `POST csr`, `POST csr/inspect` | `{ issuer, issuerKeyPassword, profile, csr, validDays, subjectAlternativeNames, takeSubjectAlternativeNames }`, and what a CSR says before that |
| `GET transparency` | the transparency log: its id, its public key, its size, its newest signed tree head, and what is wrong with it |
| `GET transparency/entries?start=&end=` | its entries, each with what its certificate is and whether the PKI still has it |
| `GET transparency/ct/v1/...` | what RFC 6962, section 4, has a log answer: `get-sth`, `get-sth-consistency`, `get-proof-by-hash`, `get-entries`, `get-roots` and `get-entry-and-proof` |

A `subject` is `{ commonName, organization, organizationalUnit, country,
state, locality }`, of which only the common name is needed. Key algorithms are
`ecc-p256`, `ecc-p384`, `ecc-p521`, `rsa-2048`, `rsa-3072` and `rsa-4096`.
`keyPassword` and `issuerKeyPassword` are optional: left out, `null` or empty,
the new key has no password, and the CA that signs is taken to have none. A
password is said back by no answer and written to no log.

A CSR is signed for its key and as what was chosen: its subject is taken over,
its alternative names where that is asked for, and nothing else it asks for -
a CSR asking to be a CA is still signed as a client's certificate. A program
that sends CSRs on its own - a backend for its charging stations - signs in
with an API key of an account in the `registrar` role.

The details of a certificate say where it is in the transparency log, with its
signed certificate timestamp. A monitor of certificate transparency is pointed
at `https://<pki>/api/v1/pki/transparency`, the log's public key and its
maximum merge delay, which is none: a certificate is in the tree as soon as it
is signed. Unlike the logs of the web, this one is read only by whoever may look
at the certificates - it names every charging station and every host - so a
monitor signs in with an API key of an account in the `operator` role. Nothing
is added from outside: there is no `add-chain`.


### Who may do what

The node's `viewer` may look at everything and `systemadmin` may do
everything. A PKI adds two resources, `authorities` - making and deleting CAs -
and `issuance` - signing certificates for servers and clients, downloading
their keys and deleting them - and two roles: `operator`, who looks at
everything and asks whether the network works, and `registrar`, who signs
certificates below the CAs that are there. The `roles` section of
`configuration.json` may add more, as on every node.


### What is not here yet

Revocation lists and OCSP, a PKCS#12 download, signed certificate timestamps
inside the certificates themselves - the precertificates of RFC 6962 - and an
automatic CSR endpoint for charging stations of its own (EST, or OCPP's
`SignCertificate` through a CSMS).


## Your participation

This software is Open Source under the **Affero GPL 3.0 license**.
We appreciate your participation in this ongoing project, and your help to
improve it and the e-mobility ICT in general. If you find bugs, want to
request a feature or send us a pull request, feel free to use the normal
GitHub features to do so. For this please read the Contributor License
Agreement carefully and send us a signed copy or use a similar free and
open license.
