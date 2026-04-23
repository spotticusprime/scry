window.ScryTopology = (function () {
    let cy = null;
    let dotNetRef = null;

    const healthColor = {
        Ok: '#198754',
        Warn: '#ffc107',
        Crit: '#dc3545',
        Error: '#dc3545',
        null: '#6c757d',
        undefined: '#6c757d',
    };

    const kindIcon = {
        vm: '🖥',
        service: '⚙',
        database: '🗄',
        domain: '🌐',
        router: '🔀',
        container: '📦',
        Domain: '🌐',
        Host: '🖥',
        Service: '⚙',
        Database: '🗄',
        CloudResource: '☁',
        Certificate: '🔒',
        Account: '👤',
        Network: '🔀',
        Unknown: '❓',
    };

    function nodeColor(health) {
        return healthColor[health] || healthColor['null'];
    }

    function buildElements(graphData) {
        const nodes = (graphData.nodes || []).map(n => ({
            data: {
                id: n.id,
                label: (kindIcon[n.kind] || '●') + ' ' + n.name,
                health: n.health,
                kind: n.kind,
                name: n.name,
            }
        }));
        const edges = (graphData.edges || []).map(e => ({
            data: {
                id: e.id,
                source: e.source,
                target: e.target,
                kind: e.kind,
            }
        }));
        return nodes.concat(edges);
    }

    function makeStyle() {
        return [
            {
                selector: 'node',
                style: {
                    'background-color': function (ele) { return nodeColor(ele.data('health')); },
                    'label': 'data(label)',
                    'color': '#fff',
                    'font-size': 12,
                    'text-valign': 'bottom',
                    'text-halign': 'center',
                    'text-margin-y': 4,
                    'width': 40,
                    'height': 40,
                    'border-width': 2,
                    'border-color': '#343a40',
                }
            },
            {
                selector: 'node:selected',
                style: {
                    'border-color': '#0d6efd',
                    'border-width': 3,
                }
            },
            {
                selector: 'edge',
                style: {
                    'width': 2,
                    'line-color': '#6c757d',
                    'target-arrow-color': '#6c757d',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',
                    'label': 'data(kind)',
                    'font-size': 10,
                    'color': '#adb5bd',
                    'text-rotation': 'autorotate',
                }
            },
            {
                selector: 'edge[kind="Resolves"], edge[kind="resolves"]',
                style: {
                    'line-style': 'dashed',
                }
            },
        ];
    }

    return {
        init: function (ref, containerId, graphData) {
            dotNetRef = ref;
            const container = document.getElementById(containerId);
            if (!container) { return; }
            if (cy) {
                cy.destroy();
                cy = null;
            }
            cy = cytoscape({
                container: container,
                elements: buildElements(graphData),
                style: makeStyle(),
                layout: { name: 'cose', animate: true, randomize: false },
            });
            cy.on('tap', 'node', function (evt) {
                const nodeId = evt.target.data('id');
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnNodeClicked', nodeId);
                }
            });
        },

        update: function (graphData) {
            if (!cy) { return; }
            cy.elements().remove();
            cy.add(buildElements(graphData));
            cy.style(makeStyle());
            cy.layout({ name: 'cose', animate: true, randomize: false }).run();
        },

        updateNodeHealth: function (nodeId, health) {
            if (!cy) { return; }
            const node = cy.getElementById(nodeId);
            if (node && node.length > 0) {
                node.data('health', health);
                node.style('background-color', nodeColor(health));
            }
        },

        destroy: function () {
            if (cy) {
                cy.destroy();
                cy = null;
            }
            dotNetRef = null;
        },
    };
})();
