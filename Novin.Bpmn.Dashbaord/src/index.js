import BpmnModeler from 'bpmn-js/lib/Modeler';
import BpmnViewer from 'bpmn-js/lib/Viewer';
import {
    BpmnPropertiesPanelModule,
    BpmnPropertiesProviderModule,
} from 'bpmn-js-properties-panel';
import customPropertiesProvider from './Providers';
import magicModdleDescriptor from './Providers/descriptors/magic.json';

// State variables
let bpmnModeler;
let currentViewer;

// Constants
const API_ENDPOINTS = {
    DEFINITION_CONTENT: '/api/bpmn/content',
    PROCESS_CONTENT: '/api/bpmn/content/process',
    EXECUTION_MAP: '/api/bpmn/execution-map',
    SAVE_DIAGRAM: '/api/bpmn/save'
};

/**
 * Initialize BPMN Modeler (editor mode)
 * @param {string} definitionKey - The BPMN definition key
 */
export function initializeModeler(definitionKey) {
    // Clear any existing viewer
    resetViewerContainer();
    
    bpmnModeler = new BpmnModeler({
        container: '#canvas',
        propertiesPanel: {
            parent: '#panel'
        },
        additionalModules: [
            BpmnPropertiesPanelModule,
            BpmnPropertiesProviderModule,
            customPropertiesProvider
        ],
        moddleExtensions: {
            magic: magicModdleDescriptor
        }
    });

    fetch(`${API_ENDPOINTS.DEFINITION_CONTENT}/${definitionKey}`)
        .then(response => {
            if (!response.ok) {
                throw new Error(`Error loading definition: ${response.statusText}`);
            }
            return response.text();
        })
        .then(bpmnXML => {
            try {
                bpmnModeler.importXML(bpmnXML);
                const canvas = bpmnModeler.get('canvas');
                canvas.zoom('fit-viewport');
            } catch (e) {
                console.error('Error parsing BPMN XML:', e);
            }
        })
        .catch(err => {
            console.error('Error loading BPMN diagram:', err);
            showErrorMessage('Failed to load BPMN diagram. Please try again later.');
        });
}

/**
 * Initialize BPMN Viewer (view mode with execution information)
 * @param {string} processId - The process instance ID
 * @param {boolean} includeVirtual - Whether to include virtual nodes/flows
 */
export function initializeViewer(processId, includeVirtual = true) {
    // Display loading state
    showLoadingState();
    
    // Clear any existing viewer
    resetViewerContainer();
    
    currentViewer = new BpmnViewer({
        container: '#canvas'
    });
    
    console.log(`Initializing viewer for process ${processId}`);
    
    // Step 1: Fetch the BPMN XML definition for this process
    fetchProcessDefinition(processId)
        .then(bpmnXML => {
            console.log('BPMN XML loaded successfully');
            return importBpmnDefinition(bpmnXML);
        })
        .then(() => {
            console.log('BPMN model imported successfully, fetching execution map');
            return fetchExecutionMapData(processId, includeVirtual);
        })
        .then(executionMap => {
            console.log('Execution map data received:', executionMap);
            applyExecutionMapToViewer(executionMap);
        })
        .catch(error => {
            console.error('Error in viewer initialization:', error);
            showErrorMessage('Failed to initialize process viewer: ' + error.message);
        })
        .finally(() => {
            hideLoadingState();
        });
}

/**
 * Fetch the BPMN process definition XML
 * @param {string} processId - The process instance ID
 * @returns {Promise<string>} - The BPMN XML
 */
function fetchProcessDefinition(processId) {
    console.log(`Fetching process definition for ${processId}`);
    
    return fetch(`${API_ENDPOINTS.PROCESS_CONTENT}/${processId}`)
        .then(response => {
            if (!response.ok) {
                console.error('API response error:', response.status, response.statusText);
                throw new Error(`Error fetching process definition: ${response.statusText}`);
            }
            
            // Check the content type of the response
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return response.json().then(data => data.value || data);
            } else {
                // Not JSON, treat as plain text (XML)
                return response.text();
            }
        })
        .then(data => {
            console.log('Process definition data received');
            return data;
        });
}

/**
 * Import BPMN XML definition into the viewer
 * @param {string} bpmnXML - The BPMN XML definition
 * @returns {Promise<void>}
 */
function importBpmnDefinition(bpmnXML) {
    return currentViewer.importXML(bpmnXML)
        .then(() => {
            const canvas = currentViewer.get('canvas');
            canvas.zoom('fit-viewport');
        });
}

/**
 * Fetch execution map data from API
 * @param {string} processId - The process instance ID
 * @param {boolean} includeVirtual - Whether to include virtual nodes/flows
 * @returns {Promise<object>} - The execution map data
 */
function fetchExecutionMapData(processId, includeVirtual = true) {
    console.log(`Fetching execution map for process ${processId}, includeVirtual=${includeVirtual}`);
    
    return fetch(`${API_ENDPOINTS.EXECUTION_MAP}/${processId}?includeVirtual=${includeVirtual}`)
        .then(response => {
            if (!response.ok) {
                console.error('API response error:', response.status, response.statusText);
                throw new Error(`Error fetching execution map: ${response.statusText}`);
            }
            
            // Check the content type of the response
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return response.json();
            } else {
                // If somehow not JSON, throw an error since we expect JSON here
                throw new Error('Execution map data is not in JSON format');
            }
        })
        .then(data => {
            console.log('Execution map data received:', data);
            // Handle both raw data and data wrapped in a value property
            return data.value || data;
        });
}

/**
 * Apply execution map data to the BPMN viewer
 * @param {object} executionMap - The execution map data
 */
function applyExecutionMapToViewer(executionMap) {
    if (!currentViewer) {
        console.error('No active viewer to apply execution map to');
        return;
    }

    const canvas = currentViewer.get('canvas');
    const elementRegistry = currentViewer.get('elementRegistry');

    // Process nodes
    if (executionMap.nodes && Array.isArray(executionMap.nodes)) {
        executionMap.nodes.forEach(node => {
            if (node.nodeId) {
                const element = elementRegistry.get(node.nodeId);
                if (element && node.isActive) {
                    applyStylesToElement(canvas, elementRegistry, element, node.nodeId, node);
                    addTooltipToElement(elementRegistry, node.nodeId, createNodeTooltipContent(node));
                }
            }
        });
    }

    // Process flows
    if (executionMap.flows && Array.isArray(executionMap.flows)) {
        executionMap.flows.forEach(flow => {
            if (flow.flowId) {
                const element = elementRegistry.get(flow.flowId);
                if (element && flow.isActive) {
                    canvas.addMarker(flow.flowId, 'active-flow');
                    const gfx = elementRegistry.getGraphics(flow.flowId);
                    applyColorToElement(gfx, "#FF8C00", 2);
                    addTooltipToElement(elementRegistry, flow.flowId, createFlowTooltipContent(flow));
                }
            }
        });
    }

    // Process waiting tokens
    if (executionMap.waitingTokens && Array.isArray(executionMap.waitingTokens)) {
        executionMap.waitingTokens.forEach(token => {
            if (token.currentElementId) {
                const element = elementRegistry.get(token.currentElementId);
                if (element) {
                    canvas.addMarker(token.currentElementId, 'waiting-token');
                    const gfx = elementRegistry.getGraphics(token.currentElementId);
                    applyColorToElement(gfx, "#BA55D3", 3);
                    addTooltipToElement(elementRegistry, token.currentElementId, createTokenTooltipContent(token, 'Waiting'));
                }
            }
        });
    }

    // Process active tokens
    if (executionMap.activeTokens && Array.isArray(executionMap.activeTokens)) {
        executionMap.activeTokens.forEach(token => {
            if (token.currentElementId) {
                const element = elementRegistry.get(token.currentElementId);
                if (element) {
                    canvas.addMarker(token.currentElementId, token.isExecutable ? 'highlight' : 'inactive');
                    const gfx = elementRegistry.getGraphics(token.currentElementId);
                    applyColorToElement(gfx, token.isExecutable ? "#1E90FF" : "#A9A9A9", token.isExecutable ? 3 : 1);
                    addTooltipToElement(elementRegistry, token.currentElementId, createTokenTooltipContent(token, 'Active'));
                }
            }
        });
    }
    
    // Process completed tokens
    if (executionMap.completedTokens && Array.isArray(executionMap.completedTokens)) {
        executionMap.completedTokens.forEach(token => {
            if (token.currentElementId) {
                const element = elementRegistry.get(token.currentElementId);
                if (element) {
                    canvas.addMarker(token.currentElementId, 'completed-node');
                    const gfx = elementRegistry.getGraphics(token.currentElementId);
                    applyColorToElement(gfx, "#228B22", 2);
                    addTooltipToElement(elementRegistry, token.currentElementId, createTokenTooltipContent(token, 'Completed'));
                }
            }
        });
    }

    // Initialize tooltips
    initializeTooltips();
}

/**
 * Apply styles to a BPMN element based on its type
 */
function applyStylesToElement(canvas, elementRegistry, element, id, node) {
    const elementType = element.type;
    const gfx = elementRegistry.getGraphics(id);
    
    if (elementType.includes('StartEvent')) {
        canvas.addMarker(id, 'start-event');
        applyColorToElement(gfx, "#228B22", 3);
    } 
    else if (elementType.includes('EndEvent')) {
        canvas.addMarker(id, 'end-event');
        applyColorToElement(gfx, "#8B0000", 3);
    }
    else if (elementType.includes('IntermediateThrowEvent') || elementType.includes('IntermediateCatchEvent')) {
        canvas.addMarker(id, 'triggered-event');
        applyColorToElement(gfx, "#FF8C00", 3);
    }
    else if (elementType.includes('BoundaryEvent')) {
        canvas.addMarker(id, 'boundary-event');
        applyColorToElement(gfx, "#6A5ACD", 3);
    }
    else if (elementType.includes('Task')) {
        if (elementType.includes('UserTask')) {
            canvas.addMarker(id, 'user-task');
            applyColorToElement(gfx, "#4682B4", 3);
        } else {
            canvas.addMarker(id, 'highlight');
            applyColorToElement(gfx, "#228B22", 2);
        }
    }
    else {
        // Default styling for other nodes
        canvas.addMarker(id, 'highlight');
        applyColorToElement(gfx, "#228B22", 2);
    }
}

/**
 * Apply color to a BPMN element
 */
function applyColorToElement(gfx, color, strokeWidth) {
    if (!gfx) {
        console.warn('No graphics element provided to applyColorToElement');
        return;
    }
    
    console.log('Applying color', color, 'to element', gfx);
    
    try {
        // Direct styling for SVG elements
        const selectors = ['path', 'rect', 'polygon', 'circle', 'polyline'];
        
        let styled = false;
        selectors.forEach(selector => {
            const elements = gfx.querySelectorAll(selector);
            if (elements.length > 0) {
                styled = true;
                elements.forEach(element => {
                    element.style.stroke = color;
                    if (strokeWidth) {
                        element.style.strokeWidth = strokeWidth + 'px';
                    }
                });
            }
        });
        
        // If no elements were styled, try direct styling (for some simple elements)
        if (!styled && gfx.tagName && selectors.includes(gfx.tagName.toLowerCase())) {
            gfx.style.stroke = color;
            if (strokeWidth) {
                gfx.style.strokeWidth = strokeWidth + 'px';
            }
        }
        
        // Add styling class for CSS rules
        if (color === "#228B22") { // Forest green (completed)
            gfx.classList.add('completed-node');
        } else if (color === "#FF8C00") { // Dark orange (active flow)
            gfx.classList.add('active-flow');
        } else if (color === "#1E90FF") { // Dodger blue (active task)
            gfx.classList.add('highlight');
        } else if (color === "#BA55D3") { // Medium orchid (waiting)
            gfx.classList.add('waiting-token');
        }
    } catch (error) {
        console.error('Error applying color to element:', error);
    }
}

/**
 * Add tooltip to a BPMN element
 */
function addTooltipToElement(elementRegistry, elementId, tooltipContent) {
    const gfx = elementRegistry.getGraphics(elementId);
    if (gfx) {
        gfx.setAttribute('title', tooltipContent.title);
        gfx.setAttribute('data-toggle', 'tooltip');
        gfx.setAttribute('data-placement', 'top');
        gfx.setAttribute('data-html', 'true');
        gfx.setAttribute('data-content', tooltipContent.content);
    }
}

/**
 * Create tooltip content for a node
 */
function createNodeTooltipContent(node) {
    return {
        title: `Node: ${node.nodeId}`,
        content: `Execution count: ${node.executionCount}<br>
                  Last execution: ${formatDateTime(node.lastExecutionTime)}`
    };
}

/**
 * Create tooltip content for a flow
 */
function createFlowTooltipContent(flow) {
    return {
        title: `Flow: ${flow.flowId}`,
        content: `Execution count: ${flow.executionCount}<br>
                  Last execution: ${formatDateTime(flow.lastExecutionTime)}`
    };
}

/**
 * Create tooltip content for a token
 */
function createTokenTooltipContent(token, tokenType) {
    return {
        title: `${tokenType} Token: ${token.currentElementId}`,
        content: `Token ID: ${token.id}<br>
                  ${token.hasOwnProperty('isExecutable') ? `Executable: ${token.isExecutable}` : ''}`
    };
}

/**
 * Format date time for display
 */
function formatDateTime(dateTimeString) {
    if (!dateTimeString) return 'N/A';
    try {
        return new Date(dateTimeString).toLocaleTimeString();
    } catch (e) {
        return dateTimeString;
    }
}

/**
 * Initialize bootstrap tooltips
 */
function initializeTooltips() {
    if (typeof $ !== 'undefined') {
        $('[data-toggle="tooltip"]').tooltip();
    }
}

/**
 * Reset viewer container
 */
function resetViewerContainer() {
    const canvasElement = document.getElementById('canvas');
    if (canvasElement) {
        canvasElement.innerHTML = '';
    }
    
    if (currentViewer) {
        try {
            currentViewer.destroy();
        } catch (e) {
            console.warn('Error destroying previous viewer:', e);
        }
    }
}

/**
 * Show loading state
 */
function showLoadingState() {
    const canvasElement = document.getElementById('canvas');
    if (canvasElement) {
        const loader = document.createElement('div');
        loader.className = 'bpmn-loader';
        loader.innerHTML = '<div class="spinner-border text-primary" role="status"><span class="sr-only">Loading...</span></div>';
        loader.style.position = 'absolute';
        loader.style.top = '50%';
        loader.style.left = '50%';
        loader.style.transform = 'translate(-50%, -50%)';
        canvasElement.appendChild(loader);
    }
}

/**
 * Hide loading state
 */
function hideLoadingState() {
    const loaderElement = document.querySelector('.bpmn-loader');
    if (loaderElement) {
        loaderElement.remove();
    }
}

/**
 * Show error message
 */
function showErrorMessage(message) {
    const canvasElement = document.getElementById('canvas');
    if (canvasElement) {
        const errorElement = document.createElement('div');
        errorElement.className = 'alert alert-danger';
        errorElement.style.margin = '20px';
        errorElement.innerText = message;
        canvasElement.appendChild(errorElement);
    }
}

/**
 * Export BPMN diagram
 */
export async function exportDiagram() {
    if (!bpmnModeler) {
        throw new Error('BPMN Modeler is not initialized');
    }
    
    try {
        const result = await bpmnModeler.saveXML({ format: true });
        return result.xml;
    } catch (error) {
        console.error('Error exporting diagram:', error);
        throw error;
    }
}

/**
 * Save diagram changes
 */
export async function saveChanges(definitionKey) {
    try {
        const updatedXML = await exportDiagram();

        const response = await fetch(API_ENDPOINTS.SAVE_DIAGRAM, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ definitionKey: definitionKey, bpmnXML: updatedXML })
        });

        if (!response.ok) {
            throw new Error(`Failed to save: ${response.statusText}`);
        }
        
        return { success: true, message: 'BPMN diagram saved successfully' };
    } catch (error) {
        console.error('Failed to save BPMN diagram:', error);
        return { success: false, message: 'Failed to save BPMN diagram' };
    }
}

/**
 * Update execution map for a process
 */
export function updateExecutionMap(processId, includeVirtual = true) {
    console.log(`Updating execution map for process ${processId}, includeVirtual=${includeVirtual}`);
    initializeViewer(processId, includeVirtual);
}
