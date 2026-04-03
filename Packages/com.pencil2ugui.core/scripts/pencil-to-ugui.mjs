#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { convertPenFileToUgui } from './pencil-to-ugui-lib.mjs';

function readArgs(argv) {
  const result = {
    file: '',
    node: null,
    out: null
  };

  for (let index = 2; index < argv.length; index += 1) {
    const arg = argv[index];
    const value = argv[index + 1];

    if (arg === '--file') {
      result.file = value;
      index += 1;
      continue;
    }

    if (arg === '--node') {
      result.node = value;
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
  if (!args.file) {
    throw new Error('Usage: node scripts/pencil-to-ugui.mjs --file <path> [--node <id>] [--out <path>]');
  }

  const ugui = convertPenFileToUgui(args.file, args.node);
  const json = `${JSON.stringify(ugui, null, 2)}\n`;

  if (args.out) {
    fs.mkdirSync(path.dirname(args.out), { recursive: true });
    fs.writeFileSync(args.out, json, 'utf8');
    process.stdout.write(`${args.out}\n`);
    return;
  }

  process.stdout.write(json);
}

main();
