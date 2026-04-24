window.ScryTopology = (function () {
    let cy = null;
    let dotNetRef = null;

    const healthColor = {
        'Ok':      '#34d399',
        'Warn':    '#fbbf24',
        'Crit':    '#f87171',
        'Error':   '#a78bfa',
        'Unknown': '#2d3342',
        null:      '#1a1d2e',
        undefined: '#1a1d2e',
    };

    const healthGlow = {
        'Ok':   '0 0 12px rgba(52,211,153,0.55)',
        'Warn': '0 0 10px rgba(251,191,36,0.45)',
        'Crit': '0 0 14px rgba(248,113,113,0.60)',
        'Error':'0 0 10px rgba(167,139,250,0.45)',
    };

    const kindIcon = {
        Domain:        '🌐',
        Host:          '🖥',
        Service:       '⚙',
        Database:      '🗄',
        CloudResource: '☁',
        Certificate:   '🔒',
        Account:       '👤',
        Network:       '🔀',
        Unknown:       '●',
    };

    function nodeColor(health) {
        return healthColor[health] ?? healthColor[null];
    }

    function buildElements(graphData) {
        const nodes = (graphData.nodes || graphData.Nodes || []).map(n => ({
            data: { id: n.id, label: (kindIcon[n.kind] || '●') + ' ' + n.name, health: n.health, kind: n.kind, name: n.name }
        }));
        const edges = (graphData.edges || graphData.Edges || []).map(e => ({
            data: { id: e.id, source: e.source, target: e.target, kind: e.kind }
        }));
        return nodes.concat(edges);
    }

    function makeStyle() {
        return [
            {
                selector: 'node',
                style: {
                    'background-color': ele => nodeColor(ele.data('health')),
                    'label': 'data(label)',
                    'color': '#94a3b8',
                    'font-size': 11,
                    'font-family': "'Inter', system-ui, sans-serif",
                    'font-weight': 500,
                    'text-valign': 'bottom',
                    'text-halign': 'center',
                    'text-margin-y': 6,
                    'width': 36,
                    'height': 36,
                    'border-width': 1.5,
                    'border-color': ele => nodeColor(ele.data('health')) === healthColor[null]
                        ? 'rgba(255,255,255,0.08)' : nodeColor(ele.data('health')),
                    'border-opacity': 0.6,
                    'shadow-color': ele => nodeColor(ele.data('health')),
                    'shadow-blur': ele => (ele.data('health') && ele.data('health') !== 'Unknown') ? 14 : 0,
                    'shadow-opacity': 0.5,
                    'text-background-color': 'transparent',
                    'transition-property': 'background-color, border-color, shadow-blur',
                    'transition-duration': '300ms',
                }
            },
            {
                selector: 'node:selected',
                style: {
                    'border-color': '#818cf8',
                    'border-width': 2.5,
                    'shadow-color': '#818cf8',
                    'shadow-blur': 18,
                    'shadow-opacity': 0.6,
                }
            },
            {
                selector: 'node:hover',
                style: {
                    'border-opacity': 1,
                    'border-width': 2,
                }
            },
            {
                selector: 'edge',
                style: {
                    'width': 1.5,
                    'line-color': 'rgba(255,255,255,0.12)',
                    'target-arrow-color': 'rgba(255,255,255,0.18)',
                    'target-arrow-shape': 'triangle',
                    'arrow-scale': 0.9,
                    'curve-style': 'bezier',
                    'label': 'data(kind)',
                    'font-size': 9,
                    'font-family': "'Inter', system-ui, sans-serif",
                    'color': 'rgba(255,255,255,0.25)',
                    'text-rotation': 'autorotate',
                    'text-margin-y': -6,
                }
            },
            {
                selector: 'edge[kind="Resolves"]',
                style: { 'line-style': 'dashed', 'line-dash-pattern': [4, 3] }
            },
            {
                selector: 'edge:selected',
                style: { 'line-color': '#818cf8', 'target-arrow-color': '#818cf8' }
            },
        ];
    }

    function initCytoscape(container, elements) {
        if (cy) { cy.destroy(); cy = null; }

        cy = cytoscape({
            container,
            elements,
            style: makeStyle(),
            layout: { name: 'cose', animate: true, randomize: false, padding: 40, nodeRepulsion: 6000, idealEdgeLength: 120 },
            minZoom: 0.2,
            maxZoom: 3,
            wheelSensitivity: 0.3,
        });

        cy.on('tap', 'node', evt => {
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnNodeClicked', evt.target.data('id'));
        });

        cy.on('tap', evt => {
            if (evt.target === cy && dotNetRef) dotNetRef.invokeMethodAsync('OnNodeClicked', '');
        });
    }

    return {
        init(ref, containerId, graphData) {
            dotNetRef = ref;
            const container = document.getElementById(containerId);
            if (!container) { return; }
            initCytoscape(container, buildElements(graphData));
        },

        update(graphData) {
            if (!cy) { return; }
            cy.elements().remove();
            cy.add(buildElements(graphData));
            cy.style(makeStyle());
            cy.layout({ name: 'cose', animate: true, randomize: false, padding: 40 }).run();
        },

        updateNodeHealth(nodeId, health) {
            if (!cy) { return; }
            const node = cy.getElementById(nodeId);
            if (node && node.length > 0) {
                node.data('health', health);
                node.style({
                    'background-color': nodeColor(health),
                    'border-color': nodeColor(health),
                    'shadow-color': nodeColor(health),
                    'shadow-blur': (health && health !== 'Unknown') ? 14 : 0,
                });
            }
        },

        destroy() {
            if (cy) { cy.destroy(); cy = null; }
            dotNetRef = null;
        },
    };
})();
