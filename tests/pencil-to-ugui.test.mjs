import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

import {
  createImportBundle,
  convertPenFileToUgui,
  convertPenNodeToUgui,
  loadPenDocument
} from '../Packages/com.pencil2ugui.core/scripts/pencil-to-ugui-lib.mjs';

const fixturePath = path.resolve('tests/fixtures/sample.pen');

test('expands refs and maps a reusable button into a layout node', () => {
  const document = loadPenDocument(fixturePath);
  const ugui = convertPenNodeToUgui(document, 'root');

  assert.equal(ugui.name, 'Screen');
  assert.equal(ugui.componentType, 'Panel');
  assert.equal(ugui.children.length, 2);

  const button = ugui.children[0];
  assert.equal(button.name, 'Btn Destructive');
  assert.equal(button.componentType, 'HorizontalLayout');
  assert.equal(button.layout.direction, 'horizontal');
  assert.deepEqual(button.layout.padding, { top: 12, right: 24, bottom: 12, left: 24 });
  assert.equal(button.children[0].componentType, 'Text');
  assert.equal(button.children[0].componentData.text, 'Delete');
});

test('maps image fills to image refs and keeps overridden size from ref instance', () => {
  const document = loadPenDocument(fixturePath);
  const ugui = convertPenNodeToUgui(document, 'root');
  const image = ugui.children[1];

  assert.equal(image.componentType, 'Image');
  assert.equal(image.componentData.imageRef, 'image.png');
  assert.deepEqual(image.rectTransform.sizeDelta, { x: 167, y: 248 });
  assert.deepEqual(image.rectTransform.anchoredPosition, { x: 426, y: -25 });
});

test('cli writes a json file for the requested node', () => {
  const outputDir = fs.mkdtempSync(path.join(os.tmpdir(), 'design2ugui-'));
  const outputPath = path.join(outputDir, 'screen.json');

  execFileSync(
    process.execPath,
    ['Packages/com.pencil2ugui.core/scripts/pencil-to-ugui.mjs', '--file', fixturePath, '--node', 'root', '--out', outputPath],
    { stdio: 'pipe' }
  );

  const written = JSON.parse(fs.readFileSync(outputPath, 'utf8'));
  assert.equal(written.name, 'Screen');
  assert.equal(written.children[0].children[0].componentData.text, 'Delete');
});

test('convertPenFileToUgui supports selecting the first top-level node by default', () => {
  const ugui = convertPenFileToUgui(fixturePath);
  assert.equal(ugui.name, 'Screen');
});

test('bundle generation extracts components and sprite assets for Unity import', () => {
  const bundle = createImportBundle(fixturePath, 'root');

  assert.equal(bundle.outputRoot, 'Assets/UI');
  assert.equal(bundle.screen.rootNode.children[0].componentType, 'PrefabInstance');
  assert.equal(bundle.screen.rootNode.children[1].componentType, 'PrefabInstance');
  assert.equal(bundle.components.length, 2);
  assert.equal(bundle.assets.length, 1);
  assert.equal(bundle.assets[0].kind, 'sprite');
  assert.equal(bundle.assets[0].targetPath, 'Assets/UI/Sprites/image.png');
  assert.ok(bundle.components.every(component => component.prefabPath.startsWith('Assets/UI/Components/')));
  assert.deepEqual(bundle.fonts, [{ family: 'Inter', weights: ['500'] }]);
});

test('bundle generation deduplicates structurally identical reusable components', () => {
  const bundle = createImportBundle('D:\\PencilProject\\1.pen', 'bi8Au');
  const buttonInstances = bundle.screen.rootNode.children.filter(child => child.name === 'Btn Destructive');

  assert.equal(buttonInstances.length, 2);
  assert.equal(buttonInstances[0].prefabKey, buttonInstances[1].prefabKey);
});

test('missing node errors include available top-level ids', () => {
  const document = loadPenDocument(fixturePath);

  assert.throws(
    () => convertPenNodeToUgui(document, 'screenRoot'),
    /Node not found: screenRoot\. Available top-level node ids: root, buttonDanger, cardArt/
  );
});
