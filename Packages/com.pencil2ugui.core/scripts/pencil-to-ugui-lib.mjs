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

function tokenizeName(value) {
  return String(value ?? '')
    .replace(/[/\\:_-]+/g, ' ')
    .replace(/[^\p{L}\p{N}\s]+/gu, ' ')
    .trim()
    .split(/\s+/)
    .filter(Boolean);
}

function toPascalCase(value, fallback = 'Node') {
  const tokens = tokenizeName(value);
  if (tokens.length === 0) {
    return fallback;
  }

  return tokens
    .slice(0, 6)
    .map(token => token.charAt(0).toUpperCase() + token.slice(1))
    .join('');
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

function getNodeLabel(node) {
  if (typeof node.name === 'string' && node.name.trim() !== '') {
    return node.name.trim();
  }

  const directTextChild = toArray(node.children).find(child => typeof child?.content === 'string' && child.content.trim() !== '');
  if (directTextChild) {
    return directTextChild.content.trim();
  }

  if (typeof node.content === 'string' && node.content.trim() !== '') {
    return node.content.trim();
  }

  return node.id ?? node.type ?? 'Node';
}

function collectTextNodes(node, bucket = []) {
  if (!node || typeof node !== 'object') {
    return bucket;
  }

  if (typeof node.content === 'string' && node.content.trim() !== '') {
    bucket.push(node.content.trim());
  }

  for (const child of toArray(node.children)) {
    collectTextNodes(child, bucket);
  }

  return bucket;
}

function countDirectChildrenByType(node) {
  const result = {
    text: 0,
    icon: 0,
    image: 0,
    container: 0
  };

  for (const child of toArray(node.children)) {
    if (child.type === 'text') {
      result.text += 1;
      continue;
    }

    if (child.type === 'icon_font') {
      result.icon += 1;
      continue;
    }

    if (extractImageRef(child) || ['rectangle', 'ellipse', 'polygon', 'path', 'line'].includes(child.type)) {
      result.image += 1;
      continue;
    }

    result.container += 1;
  }

  return result;
}

function getRepeatedChildSignatureCount(node) {
  const counts = new Map();
  for (const child of toArray(node.children)) {
    const signature = stableStringify(normalizeComponentNodeForSignature(child));
    counts.set(signature, (counts.get(signature) ?? 0) + 1);
  }

  let maxCount = 0;
  for (const count of counts.values()) {
    if (count > maxCount) {
      maxCount = count;
    }
  }

  return maxCount;
}

function isButtonLike(node) {
  if (!['frame', 'group', 'ref'].includes(node.type)) {
    return false;
  }

  const name = `${node.name ?? ''} ${node.context ?? ''}`.toLowerCase();
  const excludedPattern = /(tooltip|avatar|label|alert|accordion|switch|pagination|progress|input|select|textarea|checkbox|radio|breadcrumb|sidebar|dialog|modal|card|table|dropdown|search|otp)/;
  if (excludedPattern.test(name)) {
    return false;
  }

  const childCounts = countDirectChildrenByType(node);
  const hasVisual = !!extractImageRef(node) || (extractSolidColor(node).a > 0.001);
  const totalDirectChildren = toArray(node.children).length;
  const hasButtonContents = childCounts.text > 0 || childCounts.icon > 0;
  const nameSuggestsButton = name.includes('button') || name.includes('btn');

  if (nameSuggestsButton) {
    return hasVisual && hasButtonContents;
  }

  return hasVisual
    && hasButtonContents
    && totalDirectChildren > 0
    && totalDirectChildren <= 3
    && childCounts.container <= 1
    && node.clip !== true
    && !!inferLayoutDirection(node);
}

function isIconButtonLike(node) {
  if (!isButtonLike(node)) {
    return false;
  }

  const childCounts = countDirectChildrenByType(node);
  return childCounts.icon > 0 && childCounts.text === 0;
}

function isScrollViewLike(node) {
  if (!['frame', 'group'].includes(node.type)) {
    return false;
  }

  const name = `${node.name ?? ''} ${node.context ?? ''}`.toLowerCase();
  const direction = inferLayoutDirection(node);
  const repeatedChildCount = getRepeatedChildSignatureCount(node);
  const hasClip = node.clip === true;
  const childCount = toArray(node.children).length;
  const nameSuggestsCollection = /(list|table|feed|scroll|menu|grid|collection|sidebar)/.test(name);

  return childCount >= 2 && repeatedChildCount >= 2 && (hasClip || nameSuggestsCollection) && !!direction;
}

function determineSemanticType(node) {
  if (node.type === 'text') {
    return 'Text';
  }

  if (node.type === 'icon_font') {
    return 'Icon';
  }

  if (isIconButtonLike(node)) {
    return 'IconButton';
  }

  if (isButtonLike(node)) {
    return 'Button';
  }

  if (isScrollViewLike(node)) {
    return 'ScrollView';
  }

  const name = `${node.name ?? ''} ${node.context ?? ''}`.toLowerCase();
  if (/(dialog|modal)/.test(name)) {
    return 'Dialog';
  }

  if (/(checkbox|radio)/.test(name)) {
    return 'Toggle';
  }

  if (/(textarea|input otp|input group|input\/|search box|select group)/.test(name)) {
    return 'InputField';
  }

  if (/data table footer|table footer/.test(name)) {
    return 'TableFooter';
  }

  if (/data table header|table header/.test(name)) {
    return 'TableHeader';
  }

  if (/table column header/.test(name)) {
    return 'TableColumnHeader';
  }

  if (/table cell/.test(name)) {
    return 'TableCell';
  }

  if (/table row/.test(name)) {
    return 'TableRow';
  }

  if (/data table|table/.test(name)) {
    return 'Table';
  }

  if (/card/.test(name)) {
    return 'Card';
  }

  if (extractImageRef(node)) {
    return 'Image';
  }

  return 'Container';
}

function determineSemanticTypeForRef(referenceNode, instanceNode) {
  const instanceSemanticType = determineSemanticType(instanceNode);
  if (instanceSemanticType !== 'Container' && instanceSemanticType !== 'Image') {
    return instanceSemanticType;
  }

  return determineSemanticType(referenceNode);
}

function determineComponentType(node, semanticType = determineSemanticType(node)) {
  if (node.type === 'text' || node.type === 'icon_font') {
    return 'Text';
  }

  if (semanticType === 'Button' || semanticType === 'IconButton') {
    return 'Button';
  }

  if (semanticType === 'Dialog') {
    return 'Panel';
  }

  if (semanticType === 'Toggle') {
    return 'Toggle';
  }

  if (semanticType === 'InputField') {
    return 'InputField';
  }

  if (semanticType === 'ScrollView' || semanticType === 'Table') {
    return 'ScrollView';
  }

  if (semanticType === 'TableRow') {
    return 'HorizontalLayout';
  }

  if (semanticType === 'TableCell' || semanticType === 'TableHeader' || semanticType === 'TableFooter' || semanticType === 'TableColumnHeader') {
    return 'Panel';
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

function buildNodeName(node, semanticType, componentType) {
  const baseLabel = getNodeLabel(node);
  const textLabel = collectTextNodes(node)[0] ?? '';
  const label = textLabel || baseLabel;
  const lowerBase = baseLabel.toLowerCase();

  switch (semanticType) {
    case 'Button':
      return lowerBase.includes('button') ? toPascalCase(baseLabel, 'Button') : `Button${toPascalCase(label, 'Action')}`;
    case 'IconButton':
      return lowerBase.includes('button') ? toPascalCase(baseLabel, 'IconButton') : `IconButton${toPascalCase(label, 'Action')}`;
    case 'ScrollView':
      return `ScrollView${toPascalCase(baseLabel, 'List')}`;
    case 'Dialog':
      return `Dialog${toPascalCase(baseLabel, 'Panel')}`;
    case 'Toggle':
      return toPascalCase(baseLabel, 'Toggle');
    case 'InputField':
      return toPascalCase(baseLabel, 'InputField');
    case 'Card':
      return `Card${toPascalCase(baseLabel, 'Item')}`;
    case 'ListItem':
      return `Item${toPascalCase(baseLabel, 'Row')}`;
    case 'Table':
      return lowerBase.includes('table') ? toPascalCase(baseLabel, 'Table') : `Table${toPascalCase(baseLabel, 'Data')}`;
    case 'TableHeader':
      return lowerBase.includes('header') ? toPascalCase(baseLabel, 'Header') : `TableHeader${toPascalCase(baseLabel, 'Section')}`;
    case 'TableFooter':
      return lowerBase.includes('footer') ? toPascalCase(baseLabel, 'Footer') : `TableFooter${toPascalCase(baseLabel, 'Section')}`;
    case 'TableRow':
      return lowerBase.includes('row') ? toPascalCase(baseLabel, 'Row') : `TableRow${toPascalCase(baseLabel, 'Item')}`;
    case 'TableCell':
      return lowerBase.includes('cell') ? toPascalCase(baseLabel, 'Cell') : `TableCell${toPascalCase(baseLabel, 'Item')}`;
    case 'TableColumnHeader':
      return lowerBase.includes('column') ? toPascalCase(baseLabel, 'ColumnHeader') : `TableColumnHeader${toPascalCase(baseLabel, 'Item')}`;
    case 'Text':
      return `Text${toPascalCase(label, 'Label')}`;
    case 'Image':
      return `Image${toPascalCase(baseLabel, 'Graphic')}`;
    default:
      break;
  }

  if (componentType === 'HorizontalLayout' || componentType === 'VerticalLayout') {
    return `${componentType}${toPascalCase(baseLabel, 'Group')}`;
  }

  return toPascalCase(baseLabel, 'Node');
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

function collectSubtreeSignatures(node, context) {
  const materialized = materializeNode(node, context.index);
  const semanticType = determineSemanticType(materialized);
  const signature = stableStringify(normalizeComponentNodeForSignature(materialized));

  if (semanticType === 'Button' || semanticType === 'IconButton') {
    context.signatureCounts.set(signature, (context.signatureCounts.get(signature) ?? 0) + 1);
  }

  for (const child of toArray(materialized.children)) {
    collectSubtreeSignatures(child, context);
  }
}

function shouldAutoExtractComponent(node, semanticType, signature, context, parentNode, parentSemanticType = null) {
  if (!parentNode) {
    return false;
  }

  if (node.reusable === true) {
    return true;
  }

  if (semanticType === 'Button' || semanticType === 'IconButton') {
    return (context.signatureCounts.get(signature) ?? 0) > 1;
  }

  if (parentSemanticType === 'ScrollView') {
    return (context.signatureCounts.get(signature) ?? 0) > 1;
  }

  return false;
}

function resolveItemTemplateKey(node, context) {
  const slotIds = Array.isArray(node.slot) ? node.slot.filter(Boolean) : [];
  if (slotIds.length === 0) {
    return '';
  }

  const [firstSlotId] = slotIds;
  const slotNode = context.index.get(firstSlotId);
  if (!slotNode) {
    return '';
  }

  return collectComponent(slotNode, context);
}

function convertResolvedNodeToUgui(node, index, variables = null, parentNode = null) {
  const materialized = materializeNode(node, index);
  const semanticType = determineSemanticType(materialized);
  const componentType = determineComponentType(materialized, semanticType);
  const uguiNode = {
    sourceId: materialized.id ?? '',
    name: buildNodeName(materialized, semanticType, componentType),
    semanticType,
    templateKey: '',
    itemTemplateKey: '',
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
    const semanticType = determineSemanticTypeForRef(referenced, materialized);
    const componentType = determineComponentType(referenced, semanticType);
    return {
      sourceId: materialized.id ?? '',
      name: buildNodeName(materialized, semanticType, componentType),
      semanticType,
      templateKey: componentKey,
      itemTemplateKey: '',
      prefabKey: componentKey,
      componentType: 'PrefabInstance',
      rectTransform: buildBundleRectTransform(materialized, parentNode),
      componentData: buildComponentData(materialized, 'Panel', context.variables),
      children: []
    };
  }

  const materialized = materializeNode(node, context.index);
  const semanticType = determineSemanticType(materialized);
  const componentType = determineComponentType(materialized, semanticType);
  const signature = stableStringify(normalizeComponentNodeForSignature(materialized));
  const parentSemanticType = parentNode ? determineSemanticType(parentNode) : null;
  const repeatedInsideScrollView = parentSemanticType === 'ScrollView' && (context.signatureCounts.get(signature) ?? 0) > 1;
  const effectiveSemanticType = repeatedInsideScrollView ? 'ListItem' : semanticType;
  if (shouldAutoExtractComponent(materialized, effectiveSemanticType, signature, context, parentNode, parentSemanticType)) {
    const componentKey = collectComponent(materialized, context);
    return {
      sourceId: materialized.id ?? '',
      name: buildNodeName(materialized, effectiveSemanticType, 'PrefabInstance'),
      semanticType: effectiveSemanticType,
      templateKey: componentKey,
      itemTemplateKey: '',
      prefabKey: componentKey,
      componentType: 'PrefabInstance',
      rectTransform: buildBundleRectTransform(materialized, parentNode),
      componentData: buildComponentData(materialized, 'Panel', context.variables),
      children: []
    };
  }

  const assetKey = registerAsset(materialized, context.penFilePath, context.assetMap, context.outputRoot);
  registerFont(materialized, context.fontMap);
  const componentData = buildComponentData(materialized, componentType, context.variables);
  if (assetKey) {
    componentData.imageRef = assetKey;
  }

  const bundleNode = {
    sourceId: materialized.id ?? '',
    name: buildNodeName(materialized, effectiveSemanticType, componentType),
    semanticType: effectiveSemanticType,
    templateKey: '',
    itemTemplateKey: '',
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

  if (componentType === 'ScrollView') {
    bundleNode.itemTemplateKey = resolveItemTemplateKey(materialized, context);
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
    signatureCounts: new Map(),
    assetMap: new Map(),
    fontMap: new Map()
  };

  collectSubtreeSignatures(targetNode, context);

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
