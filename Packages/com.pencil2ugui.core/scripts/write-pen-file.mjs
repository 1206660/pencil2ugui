#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { createPenDocumentFromBundles } from './unity-to-pencil-pen-lib.mjs';

function readArgs(argv) {
  const result = {
    components: '',
    screens: '',
    audit: '',
    out: ''
  };

  for (let index = 2; index < argv.length; index += 1) {
    const arg = argv[index];
    const value = argv[index + 1];

    if (arg === '--components') {
      result.components = value;
      index += 1;
      continue;
    }

    if (arg === '--screens') {
      result.screens = value;
      index += 1;
      continue;
    }

    if (arg === '--audit') {
      result.audit = value;
      index += 1;
      continue;
    }

    if (arg === '--out') {
      result.out = value;
      index += 1;
    }
  }

  return result;
}

function main() {
  const args = readArgs(process.argv);
  if (!args.components || !args.screens || !args.audit || !args.out) {
    throw new Error(
      'Usage: node scripts/write-pen-file.mjs --components <components.bundle.json> --screens <screens.bundle.json> --audit <audit-report.json> --out <app-ui.pen>'
    );
  }

  const document = createPenDocumentFromBundles(args.components, args.screens, args.audit);
  const json = `${JSON.stringify(document, null, 2)}\n`;

  fs.mkdirSync(path.dirname(args.out), { recursive: true });
  fs.writeFileSync(args.out, json, 'utf8');
  process.stdout.write(`${args.out}\n`);
}

main();
