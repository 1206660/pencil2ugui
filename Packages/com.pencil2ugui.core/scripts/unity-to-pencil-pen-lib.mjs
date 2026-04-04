import fs from 'node:fs';

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
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

function buildTextNode(node, x = 0, y = 0) {
  const fontSize = Number.parseFloat(node?.style?.fallbacks?.fontSize ?? '16');
  const color = toColor(node?.style?.fallbacks?.textColor);
  return {
    type: 'text',
    id: sanitizeId(node?.id ?? node?.name, 'text'),
    x,
    y,
    name: toPascalCase(node?.name, 'Text'),
    width: Math.max(node?.bounds?.width ?? 160, 80),
    height: Math.max(node?.bounds?.height ?? fontSize + 8, 24),
    content: node?.content?.text || node?.content?.value || node?.name || 'Text',
    fontSize: Number.isFinite(fontSize) ? fontSize : 16,
    fills: [{ color }],
    layout: 'none'
  };
}

function buildFrameNode(node, x = 0, y = 0) {
  const fillColor = toColor(node?.style?.fallbacks?.fill);
  return {
    type: 'frame',
    id: sanitizeId(node?.id ?? node?.name, 'frame'),
    x,
    y,
    name: toPascalCase(node?.name, 'Frame'),
    width: Math.max(node?.bounds?.width ?? 240, 40),
    height: Math.max(node?.bounds?.height ?? 80, 40),
    fill: {
      type: 'solid',
      enabled: true,
      color: fillColor
    },
    stroke: buildStroke(),
    layout: 'none',
    children: []
  };
}

function convertTemplateNode(node) {
  if (!node) {
    return buildFrameNode({ name: 'Component', bounds: { width: 240, height: 80 }, style: { fallbacks: {} } });
  }

  if (node.semanticType === 'Text') {
    return buildTextNode(node);
  }

  const frame = buildFrameNode(node);
  for (const child of Array.isArray(node.children) ? node.children : []) {
    frame.children.push(convertChildNode(child));
  }

  return frame;
}

function convertChildNode(node) {
  if (node.semanticType === 'Text') {
    return buildTextNode(node, node?.bounds?.x ?? 0, node?.bounds?.y ?? 0);
  }

  const frame = buildFrameNode(node, node?.bounds?.x ?? 0, node?.bounds?.y ?? 0);
  for (const child of Array.isArray(node.children) ? node.children : []) {
    frame.children.push(convertChildNode(child));
  }
  return frame;
}

function buildComponentDefinition(component, x, y) {
  const template = convertTemplateNode(component.templateNode);
  template.id = sanitizeId(component.componentKey, template.id);
  template.name = component.componentName || template.name;
  template.reusable = true;
  template.x = x;
  template.y = y;
  return template;
}

function buildComponentPage(components) {
  const children = [];
  const spacingX = 320;
  const spacingY = 180;
  components.forEach((component, index) => {
    const column = index % 3;
    const row = Math.floor(index / 3);
    children.push(buildComponentDefinition(component, column * spacingX, row * spacingY));
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

function buildPatternPage(components) {
  const patternComponents = components.filter(component => {
    return component.semanticType === 'Dialog'
      || component.semanticType === 'Table'
      || component.semanticType === 'ScrollView';
  });

  const children = [];
  patternComponents.forEach((component, index) => {
    children.push(buildComponentDefinition(component, (index % 2) * 480, Math.floor(index / 2) * 260));
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
  const width = Math.max(instance?.bounds?.width ?? 240, 80);
  const height = Math.max(instance?.bounds?.height ?? 80, 40);
  const x = instance?.bounds?.x ?? 0;
  const y = instance?.bounds?.y ?? index * 120;

  if (instance?.componentRef) {
    return {
      type: 'ref',
      id: sanitizeId(instance.instanceKey, `instance_${index}`),
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
    id: sanitizeId(instance.instanceKey, `instance_${index}`),
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
  const children = [];
  screen.componentInstances.forEach((instance, index) => {
    children.push(buildScreenInstance(instance, index));
  });

  return {
    type: 'frame',
    id: sanitizeId(screen.screenKey, 'screen'),
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
  const children = [];
  screens.forEach((screen, index) => {
    const column = index % 2;
    const row = Math.floor(index / 2);
    children.push(buildScreenPage(screen, column * 1600, row * 1160));
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
  }, 0, index * 28));

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

export function createPenDocumentFromBundles(componentBundlePath, screenBundlePath, auditBundlePath) {
  const componentBundle = readJson(componentBundlePath);
  const screenBundle = readJson(screenBundlePath);
  const auditBundle = readJson(auditBundlePath);

  const components = Array.isArray(componentBundle.components) ? componentBundle.components : [];
  const screens = Array.isArray(screenBundle.screens) ? screenBundle.screens : [];
  const reports = Array.isArray(auditBundle.reports) ? auditBundle.reports : [];

  return {
    version: '2.10',
    children: [
      buildComponentPage(components),
      buildPatternPage(components),
      buildScreensPage(screens),
      buildAuditPage(reports)
    ]
  };
}
