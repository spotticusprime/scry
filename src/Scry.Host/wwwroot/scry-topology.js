window.ScryTopology = (function () {
    let cy = null;
    let dotNetRef = null;
    let hubConnection = null;

    const healthColor = {
        'Ok':      '#34d399',
        'Warn':    '#fbbf24',
        'Crit':    '#f87171',
        'Error':   '#a78bfa',
        'Unknown': '#2d3342',
        null:      '#1a1d2e',
        undefined: '#1a1d2e',
    };

    // Distinct shape per asset kind so nodes are identifiable without labels
    const kindShape = {
        Domain:        'ellipse',
        Host:          'rectangle',
        Service:       'round-rectangle',
        Database:      'barrel',
        CloudResource: 'diamond',
        Certificate:   'pentagon',
        Account:       'ellipse',
        Network:       'hexagon',
        Unknown:       'ellipse',
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

    function nodeShape(kind) {
        return kindShape[kind] ?? 'ellipse';
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
                    'shape': ele => nodeShape(ele.data('kind')),
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
            minZoom: 0.1,
            maxZoom: 4,
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

        fitView() {
            if (!cy) { return; }
            cy.fit(undefined, 40);
        },

        zoomIn() {
            if (!cy) { return; }
            cy.zoom({ level: cy.zoom() * 1.25, renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 } });
        },

        zoomOut() {
            if (!cy) { return; }
            cy.zoom({ level: cy.zoom() * 0.8, renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 } });
        },

        connectHub(workspaceId, ref) {
            dotNetRef = ref;
            if (hubConnection) { return; }

            const conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/probes')
                .withAutomaticReconnect()
                .build();

            conn.onclose(() => ref.invokeMethodAsync('OnHubStatusChanged', 'disconnected').catch(() => {}));
            conn.onreconnecting(() => ref.invokeMethodAsync('OnHubStatusChanged', 'reconnecting').catch(() => {}));
            conn.onreconnected(() => {
                conn.invoke('JoinWorkspace', workspaceId).catch(() => {});
                ref.invokeMethodAsync('OnHubStatusChanged', 'connected').catch(() => {});
            });

            conn.on('ProbeResult', ({ probeId, workspaceId: wsId, outcome }) => {
                ref.invokeMethodAsync('OnProbeResultPushed', probeId, wsId, outcome).catch(() => {});
            });

            conn.start()
                .then(() => conn.invoke('JoinWorkspace', workspaceId))
                .then(() => ref.invokeMethodAsync('OnHubStatusChanged', 'connected').catch(() => {}))
                .catch(() => ref.invokeMethodAsync('OnHubStatusChanged', 'error').catch(() => {}));

            hubConnection = conn;
        },

        destroy() {
            if (cy) { cy.destroy(); cy = null; }
            if (hubConnection) { hubConnection.stop().catch(() => {}); hubConnection = null; }
            dotNetRef = null;
        },
    };
})();
