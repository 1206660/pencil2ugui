import fs from 'node:fs';
import path from 'node:path';

function deepClone(value) {
  return JSON.parse(JSON.stringify(value));
}

function toArray(value) {
  return Array.isArray(value) ? value : [];
}

function defaultColor() {
  return { r: 1, g: 1, b: 1, a: 1 };
}

function normalizeHexColor(value) {
  if (typeof value !== 'string') {
    return null;
  }

  const hex = value.trim().replace('#', '');
  if (/^[0-9a-fA-F]{3}$/.test(hex)) {
    return `${hex[0]}${hex[0]}${hex[1]}${hex[1]}${hex[2]}${hex[2]}ff`;
  }

  if (/^[0-9a-fA-F]{4}$/.test(hex)) {
    return `${hex[0]}${hex[0]}${hex[1]}${hex[1]}${hex[2]}${hex[2]}${hex[3]}${hex[3]}`;
  }

  if (/^[0-9a-fA-F]{6}$/.test(hex)) {
    return `${hex}ff`;
  }

  if (/^[0-9a-fA-F]{8}$/.test(hex)) {
    return hex;
  }

  return null;
}

function resolveColorString(value, variables) {
  if (typeof value !== 'string') {
    return null;
  }

  if (value.startsWith('$')) {
    const variableName = value.slice(1);
    const variable = variables?.[variableName];
    if (!variable || variable.type !== 'color') {
      return null;
    }

    if (typeof variable.value === 'string') {
      return variable.value;
    }

    if (Array.isArray(variable.value) && variable.value.length > 0) {
      const defaultEntry = variable.value.find(entry => !entry.theme) ?? variable.value[0];
      return defaultEntry?.value ?? null;
    }

    return null;
  }

  return value;
}

function parseHexColor(value, variables = null) {
  const resolved = resolveColorString(value, variables);
  const normalized = normalizeHexColor(resolved);
  if (!normalized) {
    return defaultColor();
  }

  const r = Number.parseInt(normalized.slice(0, 2), 16) / 255;
  const g = Number.parseInt(normalized.slice(2, 4), 16) / 255;
  const b = Number.parseInt(normalized.slice(4, 6), 16) / 255;
  const a = Number.parseInt(normalized.slice(6, 8), 16) / 255;

  if ([r, g, b, a].some(channel => Number.isNaN(channel))) {
    return defaultColor();
  }

  return { r, g, b, a };
}

function normalizePadding(padding) {
  if (typeof padding === 'number') {
    return { top: padding, right: padding, bottom: padding, left: padding };
  }

  if (Array.isArray(padding) && padding.length === 2) {
    return {
      top: padding[0],
      right: padding[1],
      bottom: padding[0],
      left: padding[1]
    };
  }

  if (Array.isArray(padding) && padding.length === 4) {
    return {
      top: padding[0],
      right: padding[1],
      bottom: padding[2],
      left: padding[3]
    };
  }

  return { top: 0, right: 0, bottom: 0, left: 0 };
}

function collectNodes(children, index = new Map()) {
  for (const child of toArray(children)) {
    index.set(child.id, child);
    collectNodes(child.children, index);
  }

  return index;
}

function sanitizeName(value) {
  return String(value ?? 'Unnamed')
    .trim()
    .replace(/[<>:"/\\|?*\x00-\x1F]/g, '_')
    .replace(/\s+/g, '_')
    .replace(/_+/g, '_');
}

function stableStringify(value) {
  if (Array.isArray(value)) {
    return `[${value.map(stableStringify).join(',')}]`;
  }

  if (value && typeof value === 'object') {
    const entries = Object.keys(value)
      .sort()
      .map(key => `${JSON.stringify(key)}:${stableStringify(value[key])}`);
    return `{${entries.join(',')}}`;
  }

  return JSON.stringify(value);
}

function loadPenDocument(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function applyOverride(target, override) {
  if (!override || typeof override !== 'object') {
    return target;
  }

  const next = Array.isArray(target) ? [...target] : { ...target };
  for (const [key, value] of Object.entries(override)) {
    if (key === 'children') {
      next.children = deepClone(value);
      continue;
    }

    if (value && typeof value === 'object' && !Array.isArray(value) && next[key] && typeof next[key] === 'object' && !Array.isArray(next[key])) {
      next[key] = applyOverride(next[key], value);
      continue;
    }

    next[key] = deepClone(value);
  }

  return next;
}

function applyDescendantOverrides(node, descendants) {
  if (!descendants || typeof descendants !== 'object') {
    return node;
  }

  const cloned = deepClone(node);
  const childMap = collectNodes([cloned]);

  for (const [path, override] of Object.entries(descendants)) {
    const parts = path.split('/');
    const targetId = parts[parts.length - 1];
    const existing = childMap.get(targetId);

    if (!existing) {
      continue;
    }

    const replacement = override && typeof override === 'object' && 'type' in override
      ? applyOverride({}, override)
      : applyOverride(existing, override);

    replaceChildById(cloned, targetId, replacement);
  }

  return cloned;
}

function replaceChildById(node, targetId, replacement) {
  const children = toArray(node.children);

  for (let index = 0; index < children.length; index += 1) {
    if (children[index].id === targetId) {
      children[index] = replacement;
      node.children = children;
      return true;
    }

    if (replaceChildById(children[index], targetId, replacement)) {
      return true;
    }
  }

  return false;
}

function materializeNode(node, index) {
  if (!node) {
    throw new Error('Node is required');
  }

  if (node.type !== 'ref') {
    return deepClone(node);
  }

  const referenced = index.get(node.ref);
  if (!referenced) {
    throw new Error(`Missing reusable node: ${node.ref}`);
  }

  let resolved = deepClone(referenced);
  resolved = applyDescendantOverrides(resolved, node.descendants);

  const rootOverride = { ...node };
  delete rootOverride.type;
  delete rootOverride.ref;
  delete rootOverride.descendants;

  resolved = applyOverride(resolved, rootOverride);
  return resolved;
}

function findTargetNode(document, nodeId) {
  const allNodes = collectNodes(document.children);
  if (nodeId) {
    const node = allNodes.get(nodeId);
    if (!node) {
      const availableNodeIds = toArray(document.children)
        .map(child => child.id)
        .filter(Boolean);
      const suffix = availableNodeIds.length > 0
        ? `. Available top-level node ids: ${availableNodeIds.join(', ')}`
        : '';
      throw new Error(`Node not found: ${nodeId}${suffix}`);
    }
    return node;
  }

  const [firstChild] = toArray(document.children);
  if (!firstChild) {
    throw new Error('Document has no top-level nodes');
  }

  return firstChild;
}

function inferLayoutDirection(node) {
  if (node.layout === 'horizontal' || node.layout === 'vertical') {
    return node.layout;
  }

  if (node.layout === 'none') {
    return null;
  }

  if ('gap' in node || 'padding' in node || 'justifyContent' in node || 'alignItems' in node) {
    return 'horizontal';
  }

  return null;
}

function determineComponentType(node) {
  if (node.type === 'text' || node.type === 'icon_font') {
    return 'Text';
  }

  if (node.type === 'rectangle' || node.type === 'ellipse' || node.type === 'polygon' || node.type === 'path' || node.type === 'line') {
    return 'Image';
  }

  const direction = inferLayoutDirection(node);
  if (direction === 'horizontal') {
    return 'HorizontalLayout';
  }

  if (direction === 'vertical') {
    return 'VerticalLayout';
  }

  if (extractImageRef(node)) {
    return 'Image';
  }

  return 'Panel';
}

function extractSolidColor(node, variables = null) {
  if (typeof node.fill === 'string') {
    return parseHexColor(node.fill, variables);
  }

  if (node.fill && typeof node.fill === 'object' && !Array.isArray(node.fill) && node.fill.type === 'color' && typeof node.fill.color === 'string') {
    return parseHexColor(node.fill.color, variables);
  }

  return defaultColor();
}

function extractImageRef(node) {
  if (node.fill && typeof node.fill === 'object' && !Array.isArray(node.fill) && node.fill.type === 'image') {
    return node.fill.url ?? null;
  }

  if (Array.isArray(node.fill)) {
    const imageFill = node.fill.find(fill => fill && typeof fill === 'object' && fill.type === 'image');
    return imageFill?.url ?? null;
  }

  return null;
}

function extractText(node) {
  if (typeof node.content === 'string') {
    return node.content;
  }

  if (typeof node.iconFontName === 'string') {
    return `[icon:${node.iconFontName}]`;
  }

  return '';
}

function parseFontWeight(value) {
  const parsed = Number.parseInt(String(value ?? ''), 10);
  return Number.isNaN(parsed) ? 400 : parsed;
}

function buildLayout(node) {
  const direction = inferLayoutDirection(node);
  if (!direction) {
    return null;
  }

  return {
    direction,
    gap: typeof node.gap === 'number' ? node.gap : 0,
    padding: normalizePadding(node.padding),
    justifyContent: node.justifyContent ?? 'start',
    alignItems: node.alignItems ?? 'start'
  };
}

function buildRectTransform(node, parentNode) {
  const width = typeof node.width === 'number' ? node.width : 0;
  const height = typeof node.height === 'number' ? node.height : 0;

  if (!parentNode) {
    return {
      anchorMin: { x: 0.5, y: 0.5 },
      anchorMax: { x: 0.5, y: 0.5 },
      pivot: { x: 0.5, y: 0.5 },
      anchoredPosition: { x: 0, y: 0 },
      sizeDelta: { x: width, y: height }
    };
  }

  if (inferLayoutDirection(parentNode)) {
    return {
      anchorMin: { x: 0, y: 1 },
      anchorMax: { x: 0, y: 1 },
      pivot: { x: 0, y: 1 },
      anchoredPosition: { x: 0, y: 0 },
      sizeDelta: { x: width, y: height }
    };
  }

  return {
    anchorMin: { x: 0, y: 1 },
    anchorMax: { x: 0, y: 1 },
    pivot: { x: 0, y: 1 },
    anchoredPosition: {
      x: typeof node.x === 'number' ? node.x : 0,
      y: typeof node.y === 'number' ? -node.y : 0
    },
    sizeDelta: { x: width, y: height }
  };
}

function buildComponentData(node, componentType, variables = null) {
  return {
    color: extractSolidColor(node, variables),
    text: componentType === 'Text' ? extractText(node) : '',
    fontSize: typeof node.fontSize === 'number' ? node.fontSize : 14,
    fontFamily: typeof node.fontFamily === 'string' ? node.fontFamily : '',
    fontWeight: parseFontWeight(node.fontWeight),
    textAlign: typeof node.textAlign === 'string' ? node.textAlign : 'Left',
    letterSpacing: typeof node.letterSpacing === 'number' ? node.letterSpacing : 0,
    imageRef: extractImageRef(node),
    isScrollable: false
  };
}

function buildBundleRectTransform(node, parentNode) {
  return buildRectTransform(node, parentNode);
}

function makeComponentKey(node) {
  return `${sanitizeName(node.name ?? node.id)}__${node.id}`;
}

function makeComponentPrefabPath(node, outputRoot) {
  return `${outputRoot}/Components/${makeComponentKey(node)}.prefab`;
}

function makeScreenPrefabPath(screenName, outputRoot) {
  const safeName = sanitizeName(screenName);
  return `${outputRoot}/${safeName}/${safeName}.prefab`;
}

function registerAsset(node, penFilePath, assetMap, outputRoot) {
  const imageRef = extractImageRef(node);
  if (!imageRef) {
    return null;
  }

  const absoluteSourcePath = path.resolve(path.dirname(penFilePath), imageRef);
  const extension = path.extname(imageRef) || '.png';
  const key = sanitizeName(imageRef);
  const baseName = sanitizeName(path.basename(imageRef, extension));
  const targetPath = `${outputRoot}/Sprites/${baseName}${extension}`;

  if (!assetMap.has(key)) {
    assetMap.set(key, {
      key,
      kind: 'sprite',
      sourcePath: absoluteSourcePath,
      targetPath
    });
  }

  return key;
}

function registerFont(node, fontMap) {
  if (typeof node.fontFamily !== 'string' || node.fontFamily.trim() === '') {
    return;
  }

  const family = node.fontFamily.trim();
  const weight = String(node.fontWeight ?? '400');
  if (!fontMap.has(family)) {
    fontMap.set(family, new Set());
  }

  fontMap.get(family).add(weight);
}

function normalizeComponentNodeForSignature(node) {
  if (Array.isArray(node)) {
    return node.map(normalizeComponentNodeForSignature);
  }

  if (!node || typeof node !== 'object') {
    return node;
  }

  const normalized = {};
  for (const [key, value] of Object.entries(node)) {
    if (['id', 'x', 'y', 'reusable', 'name'].includes(key)) {
      continue;
    }

    normalized[key] = normalizeComponentNodeForSignature(value);
  }

  return normalized;
}

function convertResolvedNodeToUgui(node, index, variables = null, parentNode = null) {
  const materialized = materializeNode(node, index);
  const componentType = determineComponentType(materialized);
  const uguiNode = {
    sourceId: materialized.id ?? '',
    name: materialized.name ?? materialized.id ?? materialized.type,
    componentType,
    rectTransform: buildRectTransform(materialized, parentNode),
    componentData: buildComponentData(materialized, componentType, variables),
    children: []
  };

  const layout = buildLayout(materialized);
  if (layout) {
    uguiNode.layout = layout;
  }

  for (const child of toArray(materialized.children)) {
    uguiNode.children.push(convertResolvedNodeToUgui(child, index, variables, materialized));
  }

  return uguiNode;
}

function convertNodeToBundleNode(node, context, parentNode = null) {
  if (node.type === 'ref') {
    const referenced = context.index.get(node.ref);
    if (!referenced) {
      throw new Error(`Missing reusable node: ${node.ref}`);
    }

    const componentKey = collectComponent(referenced, context);
    const materialized = materializeNode(node, context.index);
    return {
      sourceId: materialized.id ?? '',
      name: materialized.name ?? referenced.name ?? referenced.id,
      prefabKey: componentKey,
      componentType: 'PrefabInstance',
      rectTransform: buildBundleRectTransform(materialized, parentNode),
      componentData: buildComponentData(materialized, 'Panel', context.variables),
      children: []
    };
  }

  const materialized = materializeNode(node, context.index);
  const componentType = determineComponentType(materialized);
  const assetKey = registerAsset(materialized, context.penFilePath, context.assetMap, context.outputRoot);
  registerFont(materialized, context.fontMap);
  const componentData = buildComponentData(materialized, componentType, context.variables);
  if (assetKey) {
    componentData.imageRef = assetKey;
  }

  const bundleNode = {
    sourceId: materialized.id ?? '',
    name: materialized.name ?? materialized.id ?? materialized.type,
    prefabKey: '',
    componentType,
    rectTransform: buildBundleRectTransform(materialized, parentNode),
    componentData,
    children: []
  };

  const layout = buildLayout(materialized);
  if (layout) {
    bundleNode.layout = layout;
  }

  for (const child of toArray(materialized.children)) {
    bundleNode.children.push(convertNodeToBundleNode(child, context, materialized));
  }

  return bundleNode;
}

function collectComponent(componentNode, context) {
  const signature = stableStringify(normalizeComponentNodeForSignature(componentNode));
  const existingKey = context.componentSignatureMap.get(signature);
  if (existingKey) {
    return existingKey;
  }

  const componentKey = makeComponentKey(componentNode);
  const rootNode = convertNodeToBundleNode(componentNode, context, null);
  context.componentMap.set(componentKey, {
    key: componentKey,
    name: componentNode.name ?? componentNode.id,
    prefabPath: makeComponentPrefabPath(componentNode, context.outputRoot),
    rootNode
  });
  context.componentSignatureMap.set(signature, componentKey);

  return componentKey;
}

function convertPenNodeToUgui(document, nodeId) {
  const index = collectNodes(document.children);
  const targetNode = findTargetNode(document, nodeId);
  return convertResolvedNodeToUgui(targetNode, index, document.variables ?? {});
}

function convertPenFileToUgui(filePath, nodeId = null) {
  const document = loadPenDocument(filePath);
  return convertPenNodeToUgui(document, nodeId);
}

function createImportBundle(filePath, nodeId = null, outputRoot = 'Assets/UI') {
  const document = loadPenDocument(filePath);
  const index = collectNodes(document.children);
  const targetNode = findTargetNode(document, nodeId);
  const context = {
    index,
    variables: document.variables ?? {},
    penFilePath: filePath,
    outputRoot,
    componentMap: new Map(),
    componentSignatureMap: new Map(),
    assetMap: new Map(),
    fontMap: new Map()
  };

  const rootNode = convertNodeToBundleNode(targetNode, context, null);
  const screenName = targetNode.name ?? targetNode.id ?? 'Screen';

  return {
    version: '1.0',
    outputRoot,
    source: {
      penFile: filePath,
      nodeId: targetNode.id ?? '',
      screenName
    },
    assets: Array.from(context.assetMap.values()),
    fonts: Array.from(context.fontMap.entries()).map(([family, weights]) => ({
      family,
      weights: Array.from(weights).sort()
    })),
    components: Array.from(context.componentMap.values()),
    screen: {
      name: screenName,
      prefabPath: makeScreenPrefabPath(screenName, outputRoot),
      rootNode
    }
  };
}

export {
  createImportBundle,
  convertPenFileToUgui,
  convertPenNodeToUgui,
  loadPenDocument
};
