import fs from 'node:fs';
import path from 'node:path';

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function ensureDirectory(filePath) {
  fs.mkdirSync(filePath, { recursive: true });
}

function tokenize(value) {
  return String(value ?? '')
    .replace(/[/\\:_+.-]+/g, ' ')
    .replace(/[^\p{L}\p{N}\s]+/gu, ' ')
    .trim()
    .split(/\s+/)
    .filter(Boolean);
}

function toPascalCase(value, fallback = 'Node') {
  const tokens = tokenize(value);
  if (tokens.length === 0) {
    return fallback;
  }

  return tokens
    .slice(0, 8)
    .map(token => token.charAt(0).toUpperCase() + token.slice(1))
    .join('');
}

function sanitizeId(value, fallback = 'node') {
  const raw = String(value ?? fallback)
    .replace(/[^A-Za-z0-9_-]+/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_+|_+$/g, '');
  return raw || fallback;
}

function makeUniqueId(value, assetContext, fallback = 'node') {
  const baseId = sanitizeId(value, fallback);
  if (!assetContext?.nodeIds) {
    return baseId;
  }

  const currentCount = assetContext.nodeIds.get(baseId) || 0;
  assetContext.nodeIds.set(baseId, currentCount + 1);
  return currentCount === 0 ? baseId : `${baseId}_${currentCount}`;
}

function toColor(fill) {
  if (typeof fill !== 'string') {
    return '#FFFFFF';
  }

  const hex = fill.trim().replace('#', '');
  if (/^[0-9a-fA-F]{6}$/.test(hex) || /^[0-9a-fA-F]{8}$/.test(hex)) {
    return `#${hex.slice(0, 6)}`;
  }

  return '#FFFFFF';
}

function buildStroke() {
  return {
    align: 'inside',
    thickness: 1
  };
}

function createAssetContext(options = {}) {
  const outputPenPath = options.outputPenPath || '';
  const outputDirectory = outputPenPath ? path.dirname(path.resolve(outputPenPath)) : '';
  const imagesDirectory = outputDirectory ? path.join(outputDirectory, 'images') : '';

  if (imagesDirectory) {
    ensureDirectory(imagesDirectory);
  }

  return {
    projectRoot: options.projectRoot ? path.resolve(options.projectRoot) : '',
    outputDirectory,
    imagesDirectory,
    copiedImages: new Map(),
    nodeIds: new Map()
  };
}

function sanitizeFileStem(value, fallback = 'image') {
  const stem = String(value ?? fallback)
    .replace(/[^A-Za-z0-9_-]+/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_+|_+$/g, '');
  return stem || fallback;
}

function resolveUnityAssetPath(assetPath, assetContext) {
  if (!assetPath || !assetContext.projectRoot) {
    return '';
  }

  const normalizedAssetPath = String(assetPath).replace(/[\\/]+/g, path.sep);
  return path.resolve(assetContext.projectRoot, normalizedAssetPath);
}

function copyImageAsset(assetPath, node, assetContext) {
  if (!assetPath || !assetContext.imagesDirectory) {
    return null;
  }

  if (assetContext.copiedImages.has(assetPath)) {
    return assetContext.copiedImages.get(assetPath);
  }

  const sourcePath = resolveUnityAssetPath(assetPath, assetContext);
  if (!sourcePath || !fs.existsSync(sourcePath)) {
    return null;
  }

  const extension = path.extname(sourcePath) || '.png';
  const baseName = sanitizeFileStem(path.basename(sourcePath, extension), sanitizeFileStem(node?.name, 'image'));
  let fileName = `${baseName}${extension}`;
  let targetPath = path.join(assetContext.imagesDirectory, fileName);
  let suffix = 1;

  while (fs.existsSync(targetPath) && !assetContext.copiedImages.has(assetPath)) {
    fileName = `${baseName}_${suffix}${extension}`;
    targetPath = path.join(assetContext.imagesDirectory, fileName);
    suffix += 1;
  }

  fs.copyFileSync(sourcePath, targetPath);
  const relativeUrl = `images\\${fileName}`;
  assetContext.copiedImages.set(assetPath, relativeUrl);
  return relativeUrl;
}

function buildImageFill(node, assetContext) {
  const assetPath = node?.content?.imageRef || node?.style?.spriteRef || '';
  const relativeUrl = copyImageAsset(assetPath, node, assetContext);
  if (!relativeUrl) {
    return null;
  }

  return {
    type: 'image',
    enabled: true,
    url: relativeUrl,
    mode: 'fill'
  };
}

function isTextLikeNode(node) {
  return node?.semanticType === 'Text'
    || node?.componentType === 'Text'
    || Boolean(node?.content?.text)
    || Boolean(node?.content?.value);
}

function buildTextNode(node, x = 0, y = 0, assetContext) {
  const fontSize = Number.parseFloat(node?.style?.fallbacks?.fontSize ?? '16');
  const color = toColor(node?.style?.fallbacks?.textColor);
  return {
    type: 'text',
    id: makeUniqueId(node?.id ?? node?.name, assetContext, 'text'),
    x,
    y,
    name: toPascalCase(node?.name, 'Text'),
    width: Math.max(node?.bounds?.width ?? 160, 80),
    height: Math.max(node?.bounds?.height ?? fontSize + 8, 24),
    content: node?.content?.text || node?.content?.value || node?.name || 'Text',
    fill: `${color.toLowerCase()}ff`,
    textGrowth: 'fixed-width',
    fontFamily: 'Inter',
    fontSize: Number.isFinite(fontSize) ? fontSize : 16,
    fontWeight: 'normal'
  };
}

function buildFrameNode(node, x = 0, y = 0, assetContext) {
  const hasSolidFill = typeof node?.style?.fallbacks?.fill === 'string' && node.style.fallbacks.fill.trim() !== '';
  const fillColor = toColor(node?.style?.fallbacks?.fill);
  const imageFill = buildImageFill(node, assetContext);
  const isPlainContainer = !imageFill && node?.componentType === 'Panel';
  return {
    type: 'frame',
    id: makeUniqueId(node?.id ?? node?.name, assetContext, 'frame'),
    x,
    y,
    name: toPascalCase(node?.name, 'Frame'),
    width: Math.max(node?.bounds?.width ?? 240, 40),
    height: Math.max(node?.bounds?.height ?? 80, 40),
    fill: imageFill ?? {
      type: 'solid',
      enabled: !isPlainContainer && hasSolidFill,
      color: fillColor
    },
    stroke: buildStroke(),
    layout: 'none',
    children: []
  };
}

function convertTemplateNode(node, assetContext) {
  if (!node) {
    return buildFrameNode({ name: 'Component', bounds: { width: 240, height: 80 }, style: { fallbacks: {} } }, 0, 0, assetContext);
  }

  if (isTextLikeNode(node)) {
    return buildTextNode(node, 0, 0, assetContext);
  }

  const frame = buildFrameNode(node, 0, 0, assetContext);
  for (const child of Array.isArray(node.children) ? node.children : []) {
    frame.children.push(convertChildNode(child, assetContext));
  }

  return frame;
}

function convertChildNode(node, assetContext) {
  if (isTextLikeNode(node)) {
    return buildTextNode(node, node?.bounds?.x ?? 0, node?.bounds?.y ?? 0, assetContext);
  }

  const frame = buildFrameNode(node, node?.bounds?.x ?? 0, node?.bounds?.y ?? 0, assetContext);
  for (const child of Array.isArray(node.children) ? node.children : []) {
    frame.children.push(convertChildNode(child, assetContext));
  }
  return frame;
}

function buildComponentDefinition(component, x, y, assetContext) {
  const template = convertTemplateNode(component.templateNode, assetContext);
  template.id = makeUniqueId(component.componentKey, assetContext, template.id);
  template.name = component.componentName || template.name;
  template.reusable = true;
  template.x = x;
  template.y = y;
  return template;
}

function buildComponentPage(components, assetContext) {
  const children = [];
  const spacingX = 320;
  const spacingY = 180;
  components.forEach((component, index) => {
    const column = index % 3;
    const row = Math.floor(index / 3);
    children.push(buildComponentDefinition(component, column * spacingX, row * spacingY, assetContext));
  });

  return {
    type: 'frame',
    id: 'Library',
    x: 0,
    y: 0,
    name: 'Library',
    width: 1440,
    height: Math.max(900, Math.ceil(Math.max(components.length, 1) / 3) * spacingY + 160),
    layout: 'none',
    children
  };
}

function buildPatternPage(components, assetContext) {
  const patternComponents = components.filter(component => {
    return component.semanticType === 'Dialog'
      || component.semanticType === 'Table'
      || component.semanticType === 'ScrollView';
  });

  const children = [];
  patternComponents.forEach((component, index) => {
    children.push(buildComponentDefinition(component, (index % 2) * 480, Math.floor(index / 2) * 260, assetContext));
  });

  return {
    type: 'frame',
    id: 'Patterns',
    x: 1600,
    y: 0,
    name: 'Patterns',
    width: 1440,
    height: Math.max(900, Math.ceil(Math.max(patternComponents.length, 1) / 2) * 260 + 160),
    layout: 'none',
    children
  };
}

function buildScreenInstance(instance, index) {
  const assetContext = arguments[2];
  const width = Math.max(instance?.bounds?.width ?? 240, 80);
  const height = Math.max(instance?.bounds?.height ?? 80, 40);
  const x = instance?.bounds?.x ?? 0;
  const y = instance?.bounds?.y ?? index * 120;

  if (instance?.componentRef) {
    return {
      type: 'ref',
      id: makeUniqueId(instance.instanceKey, assetContext, `instance_${index}`),
      ref: sanitizeId(instance.componentRef, 'component'),
      x,
      y,
      name: toPascalCase(instance.name, 'Instance'),
      width,
      height
    };
  }

  return {
    type: 'frame',
    id: makeUniqueId(instance.instanceKey, assetContext, `instance_${index}`),
    x,
    y,
    name: toPascalCase(instance.name, 'Frame'),
    width,
    height,
    fill: {
      type: 'solid',
      enabled: true,
      color: '#E5E7EB'
    },
    stroke: buildStroke(),
    layout: 'none'
  };
}

function buildScreenPage(screen, x, y) {
  const assetContext = arguments[3];
  const children = [];
  screen.componentInstances.forEach((instance, index) => {
    children.push(buildScreenInstance(instance, index, assetContext));
  });

  return {
    type: 'frame',
    id: makeUniqueId(screen.screenKey, assetContext, 'screen'),
    x,
    y,
    name: screen.rootFrameName || screen.screenName || 'Screen',
    width: 1440,
    height: 1024,
    clip: true,
    stroke: buildStroke(),
    layout: 'none',
    children
  };
}

function buildScreensPage(screens) {
  const assetContext = arguments[1];
  const children = [];
  screens.forEach((screen, index) => {
    const column = index % 2;
    const row = Math.floor(index / 2);
    children.push(buildScreenPage(screen, column * 1600, row * 1160, assetContext));
  });

  return {
    type: 'frame',
    id: 'Screens',
    x: 0,
    y: 1200,
    name: 'Screens',
    width: 3200,
    height: Math.max(1200, Math.ceil(Math.max(screens.length, 1) / 2) * 1160 + 80),
    layout: 'none',
    children
  };
}

function buildAuditPage(reports) {
  const issues = reports.flatMap(report => Array.isArray(report.issues) ? report.issues : []);
  const children = issues.slice(0, 60).map((issue, index) => buildTextNode({
    id: `${issue.code}_${index}`,
    name: issue.code || 'Issue',
    content: { text: `[${issue.severity}] ${issue.code}: ${issue.message}` },
    style: { fallbacks: { fontSize: '14', textColor: issue.severity === 'error' ? '#DC2626' : '#D97706' } },
    bounds: { width: 1200, height: 24 }
  }, 0, index * 28, null));

  return {
    type: 'frame',
    id: 'Audit',
    x: 3200,
    y: 0,
    name: 'Audit',
    width: 1440,
    height: Math.max(900, issues.length * 28 + 120),
    layout: 'none',
    children
  };
}

function buildScreenPageFromNode(screenAsset, x, y, assetContext) {
  const rootNode = screenAsset?.rootNode;
  const children = Array.isArray(rootNode?.children)
    ? rootNode.children.map(child => convertChildNode(child, assetContext))
    : [];

  return {
    type: 'frame',
    id: makeUniqueId(screenAsset?.assetKey ?? rootNode?.id, assetContext, 'screen'),
    x,
    y,
    name: rootNode?.name || screenAsset?.assetPath || 'Screen',
    width: Math.max(rootNode?.bounds?.width ?? 1440, 1440),
    height: Math.max(rootNode?.bounds?.height ?? 1024, 1024),
    clip: true,
    stroke: buildStroke(),
    layout: 'none',
    children
  };
}

function buildScreensPageFromScan(scanAssets, assetContext) {
  const screens = scanAssets.filter(asset => asset?.assetType === 'screen' && asset?.rootNode);
  const children = [];

  screens.forEach((screenAsset, index) => {
    const column = index % 2;
    const row = Math.floor(index / 2);
    children.push(buildScreenPageFromNode(screenAsset, column * 1600, row * 1160, assetContext));
  });

  return {
    type: 'frame',
    id: 'Screens',
    x: 0,
    y: 1200,
    name: 'Screens',
    width: 3200,
    height: Math.max(1200, Math.ceil(Math.max(screens.length, 1) / 2) * 1160 + 80),
    layout: 'none',
    children
  };
}

export function createPenDocumentFromBundles(componentBundlePath, screenBundlePath, auditBundlePath, options = {}) {
  const componentBundle = readJson(componentBundlePath);
  const screenBundle = readJson(screenBundlePath);
  const auditBundle = readJson(auditBundlePath);
  const assetContext = createAssetContext(options);
  const scanDocument = options.scanFilePath && fs.existsSync(options.scanFilePath)
    ? readJson(options.scanFilePath)
    : null;

  const components = Array.isArray(componentBundle.components) ? componentBundle.components : [];
  const screens = Array.isArray(screenBundle.screens) ? screenBundle.screens : [];
  const reports = Array.isArray(auditBundle.reports) ? auditBundle.reports : [];
  const scanAssets = Array.isArray(scanDocument?.assets) ? scanDocument.assets : [];
  const screensPage = scanAssets.length > 0
    ? buildScreensPageFromScan(scanAssets, assetContext)
    : buildScreensPage(screens);

  return {
    version: '2.10',
    children: [
      buildComponentPage(components, assetContext),
      buildPatternPage(components, assetContext),
      screensPage,
      buildAuditPage(reports)
    ]
  };
}
