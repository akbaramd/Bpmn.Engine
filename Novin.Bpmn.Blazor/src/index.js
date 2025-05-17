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
let refreshIntervalId = null;
let currentProcessInstanceId = null;

// Constants - aligned with BpmnApiController.cs endpoints
const API_ENDPOINTS = {
    INSTANCE: '/api/bpmn/instance',                 // GET /{processInstanceId}
    EXECUTION_MAP: '/api/bpmn/execution-map',       // GET /{processInstanceId}
    ACTIVE_CONTEXTS: '/api/bpmn/active-contexts',   // GET /{processInstanceId}
    PROCESS_CONTENT: '/api/bpmn/content/process',   // GET /{processInstanceId}
    ALL_PROCESSES: '/api/bpmn/all',                 // GET
    SAVE_DIAGRAM: '/api/bpmn/save'                  // POST
};

// Element state colors
const ELEMENT_COLORS = {
    active: { stroke: '#4CAF50', fill: 'rgba(76, 175, 80, 0.2)', strokeWidth: 3 },
    completed: { stroke: '#2196F3', fill: 'rgba(33, 150, 243, 0.2)', strokeWidth: 2 },
    waiting: { stroke: '#FF9800', fill: 'rgba(255, 152, 0, 0.2)', strokeWidth: 2 },
    error: { stroke: '#F44336', fill: 'rgba(244, 67, 54, 0.2)', strokeWidth: 2 },
    default: { stroke: '#9E9E9E', fill: 'white', strokeWidth: 1 }
};

// Element type styles
const ELEMENT_TYPE_STYLES = {
    'bpmn:StartEvent': { ...ELEMENT_COLORS.default, stroke: '#43A047' },
    'bpmn:EndEvent': { ...ELEMENT_COLORS.default, stroke: '#E53935' },
    'bpmn:Task': { ...ELEMENT_COLORS.default },
    'bpmn:UserTask': { ...ELEMENT_COLORS.default, stroke: '#1E88E5' },
    'bpmn:ServiceTask': { ...ELEMENT_COLORS.default, stroke: '#7B1FA2' },
    'bpmn:ExclusiveGateway': { ...ELEMENT_COLORS.default, stroke: '#FFA000' },
    'bpmn:ParallelGateway': { ...ELEMENT_COLORS.default, stroke: '#00897B' },
    'bpmn:SequenceFlow': { ...ELEMENT_COLORS.default, strokeWidth: 1 }
};

/**
 * Initialize BPMN Modeler (editor mode)
 * @param {string} definitionKey - The BPMN definition key
 */
export function initializeModeler(definitionKey) {
    // Stop any active refresh intervals
    stopRefreshInterval();
    currentProcessInstanceId = null;
    
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

    // Load diagram XML from static file or API
    loadDiagramXml(definitionKey)
        .then(bpmnXML => {
            importDiagram(bpmnModeler, bpmnXML);
        })
        .catch(error => {
            console.error('Error loading BPMN diagram:', error);
            showErrorMessage(`Failed to load BPMN diagram: ${error.message}`);
        });
}

/**
 * Initialize BPMN Viewer (view mode with execution information)
 * @param {string} processInstanceId - The process instance ID
 * @param {boolean} autoRefresh - Whether to automatically refresh the execution map
 * @param {number} refreshInterval - Refresh interval in milliseconds (default: 5000ms)
 */
export function initializeViewer(processInstanceId, autoRefresh = true, refreshInterval = 5000) {
    // Stop any active refresh intervals
    stopRefreshInterval();
    
    // Set current process instance ID
    currentProcessInstanceId = processInstanceId;
    
    // Display loading state
    showLoadingState();
    
    // Clear any existing viewer
    resetViewerContainer();
    
    // Initialize viewer
    currentViewer = new BpmnViewer({
        container: '#canvas'
    });
    
    console.log(`Initializing viewer for process ${processInstanceId}`);
    
    // Load process XML and execution data
    loadProcessData(processInstanceId)
        .finally(() => {
            hideLoadingState();
            
            // Set up automatic refresh if enabled
            if (autoRefresh && processInstanceId) {
                refreshIntervalId = setInterval(() => {
                    if (currentProcessInstanceId) {
                        refreshExecutionData(currentProcessInstanceId);
                    } else {
                        stopRefreshInterval();
                    }
                }, refreshInterval);
            }
        });
}

/**
 * Load process data (XML and execution map)
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<void>}
 */
async function loadProcessData(processInstanceId) {
    try {
        // Step 1: Fetch the BPMN XML definition for this process
        const bpmnXML = await fetchProcessDefinition(processInstanceId);
        console.log('Process XML loaded successfully');
        
        // Step 2: Import the BPMN XML into the viewer
        await importDiagram(currentViewer, bpmnXML);
        console.log('Process model imported successfully');
        
        // Step 3: Fetch and apply execution data
        await refreshExecutionData(processInstanceId);
    } catch (error) {
        console.error('Error loading process data:', error);
        showErrorMessage(`Failed to load process data: ${error.message}`);
        throw error;
    }
}

/**
 * Refresh execution data for a process
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<void>}
 */
async function refreshExecutionData(processInstanceId) {
    try {
        // Fetch execution map
        const executionMap = await fetchExecutionMap(processInstanceId);
        console.log('Execution map loaded successfully');
        
        // Fetch active contexts
        const activeContexts = await fetchActiveContexts(processInstanceId);
        console.log('Active contexts loaded successfully');
        
        // Apply execution data to viewer
        applyExecutionData(executionMap, activeContexts);
    } catch (error) {
        console.error('Error refreshing execution data:', error);
        // Don't show error message on refresh to avoid disrupting the user experience
    }
}

/**
 * Stop the refresh interval
 */
function stopRefreshInterval() {
    if (refreshIntervalId) {
        clearInterval(refreshIntervalId);
        refreshIntervalId = null;
    }
}

/**
 * Load diagram XML from static file or API
 * @param {string} definitionKey - The definition key or filename
 * @returns {Promise<string>} - The BPMN XML
 */
async function loadDiagramXml(definitionKey) {
    try {
        // Try to load from API first (if it's a URL)
        if (definitionKey.includes('/')) {
            const response = await fetch(definitionKey);
            if (!response.ok) {
                throw new Error(`Error loading definition: ${response.statusText}`);
            }
            return await response.text();
        }
        
        // Otherwise load from static file
        const response = await fetch(`/${definitionKey}.bpmn`);
        if (!response.ok) {
            throw new Error(`Error loading definition: ${response.statusText}`);
        }
        return await response.text();
    } catch (error) {
        console.error('Error loading diagram XML:', error);
        throw error;
    }
}

/**
 * Import BPMN XML definition into the viewer
 * @param {object} viewer - The BPMN viewer or modeler instance
 * @param {string} bpmnXML - The BPMN XML definition
 * @returns {Promise<void>}
 */
async function importDiagram(viewer, bpmnXML) {
    try {
        const result = await viewer.importXML(bpmnXML);
        const canvas = viewer.get('canvas');
        canvas.zoom('fit-viewport');
        return result;
    } catch (error) {
        console.error('Error importing BPMN XML:', error);
        throw error;
    }
}

/**
 * Fetch the process instance details
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<object>} - The process instance details
 */
async function fetchProcessInstance(processInstanceId) {
    console.log(`Fetching process instance details for ${processInstanceId}`);
    
    try {
        const response = await fetch(`${API_ENDPOINTS.INSTANCE}/${processInstanceId}`);
        if (!response.ok) {
            throw new Error(`Error fetching process instance: ${response.statusText}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching process instance:', error);
        throw error;
    }
}

/**
 * Fetch the BPMN process definition XML
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<string>} - The BPMN XML
 */
async function fetchProcessDefinition(processInstanceId) {
    console.log(`Fetching process definition for ${processInstanceId}`);
    
    try {
        const response = await fetch(`${API_ENDPOINTS.PROCESS_CONTENT}/${processInstanceId}`);
        if (!response.ok) {
            throw new Error(`Error fetching process definition: ${response.statusText}`);
        }
        
        // Process definition is returned as XML
        return await response.text();
    } catch (error) {
        console.error('Error fetching process definition:', error);
        throw error;
    }
}

/**
 * Fetch execution map data from API
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<object>} - The execution map data
 */
async function fetchExecutionMap(processInstanceId) {
    console.log(`Fetching execution map for process ${processInstanceId}`);
    
    try {
        const response = await fetch(`${API_ENDPOINTS.EXECUTION_MAP}/${processInstanceId}`);
        if (!response.ok) {
            throw new Error(`Error fetching execution map: ${response.statusText}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching execution map:', error);
        throw error;
    }
}

/**
 * Fetch active execution contexts
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<Array>} - The active execution contexts
 */
async function fetchActiveContexts(processInstanceId) {
    console.log(`Fetching active contexts for process ${processInstanceId}`);
    
    try {
        const response = await fetch(`${API_ENDPOINTS.ACTIVE_CONTEXTS}/${processInstanceId}`);
        if (!response.ok) {
            throw new Error(`Error fetching active contexts: ${response.statusText}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching active contexts:', error);
        throw error;
    }
}

/**
 * Fetch all processes
 * @returns {Promise<Array>} - List of all processes
 */
async function fetchAllProcesses() {
    console.log('Fetching all processes');
    
    try {
        const response = await fetch(API_ENDPOINTS.ALL_PROCESSES);
        if (!response.ok) {
            throw new Error(`Error fetching all processes: ${response.statusText}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching all processes:', error);
        throw error;
    }
}

/**
 * Apply execution data to the BPMN viewer
 * @param {object} executionMap - The execution trace map from API
 * @param {Array} activeContexts - The active execution contexts
 */
function applyExecutionData(executionMap, activeContexts) {
    if (!currentViewer) {
        console.error('No active viewer to apply execution data to');
        return;
    }

    const canvas = currentViewer.get('canvas');
    const elementRegistry = currentViewer.get('elementRegistry');

    // Reset all elements to default appearance
    resetElementStyles(elementRegistry, canvas);
    
    // Process execution traces
    if (executionMap && executionMap.traces && Array.isArray(executionMap.traces)) {
        // First pass: Mark all completed elements
        executionMap.traces.forEach(trace => {
            if (trace.path && Array.isArray(trace.path)) {
                trace.path.forEach(elementId => {
                    const element = elementRegistry.get(elementId);
                    if (element) {
                        // Mark as completed unless it's the current element of an active trace
                        if (!(elementId === trace.currentElementId && trace.state === 'Active')) {
                            markElementAsCompleted(canvas, elementRegistry, element, elementId);
                        }
                    }
                });
            }
        });
        
        // Second pass: Mark current elements for active traces
        executionMap.traces.forEach(trace => {
            if (trace.currentElementId && trace.state === 'Active') {
                const element = elementRegistry.get(trace.currentElementId);
                if (element) {
                    markElementAsActive(canvas, elementRegistry, element, trace.currentElementId, trace);
                }
            }
        });
    }
    
    // Process active contexts to highlight current elements
    if (activeContexts && Array.isArray(activeContexts)) {
        activeContexts.forEach(context => {
            if (context.currentElementId) {
                const element = elementRegistry.get(context.currentElementId);
                if (element) {
                    markElementAsActive(canvas, elementRegistry, element, context.currentElementId, {
                        executionId: context.contextId,
                        state: context.state,
                        isExecutable: true
                    });
                }
            }
        });
    }

    // Initialize tooltips
    initializeTooltips();
}

/**
 * Reset all element styles to default
 * @param {object} elementRegistry - The BPMN.js element registry
 * @param {object} canvas - The BPMN.js canvas
 */
function resetElementStyles(elementRegistry, canvas) {
    elementRegistry.forEach(element => {
        // Remove all markers
        canvas.removeMarker(element.id, 'highlight');
        canvas.removeMarker(element.id, 'active');
        canvas.removeMarker(element.id, 'completed');
        canvas.removeMarker(element.id, 'waiting');
        canvas.removeMarker(element.id, 'error');
        
        // Reset to default style based on element type
        const elementType = element.type;
        const defaultStyle = ELEMENT_TYPE_STYLES[elementType] || ELEMENT_COLORS.default;
        
        const gfx = elementRegistry.getGraphics(element.id);
        if (gfx) {
            applyColorToElement(gfx, defaultStyle.stroke, defaultStyle.fill, defaultStyle.strokeWidth);
        }
    });
}

/**
 * Mark an element as completed
 * @param {object} canvas - The BPMN.js canvas
 * @param {object} elementRegistry - The BPMN.js element registry
 * @param {object} element - The BPMN element
 * @param {string} elementId - The element ID
 */
function markElementAsCompleted(canvas, elementRegistry, element, elementId) {
    canvas.addMarker(elementId, 'completed');
    const gfx = elementRegistry.getGraphics(elementId);
    if (gfx) {
        applyColorToElement(gfx, ELEMENT_COLORS.completed.stroke, ELEMENT_COLORS.completed.fill, ELEMENT_COLORS.completed.strokeWidth);
    }
    
    // Add tooltip with execution information
    addTooltipToElement(elementRegistry, elementId, {
        title: `Completed: ${elementId}`,
        content: `Element has been executed`
    });
}

/**
 * Mark an element as active
 * @param {object} canvas - The BPMN.js canvas
 * @param {object} elementRegistry - The BPMN.js element registry
 * @param {object} element - The BPMN element
 * @param {string} elementId - The element ID
 * @param {object} trace - The execution trace data
 */
function markElementAsActive(canvas, elementRegistry, element, elementId, trace) {
    canvas.addMarker(elementId, 'active');
    const gfx = elementRegistry.getGraphics(elementId);
    if (gfx) {
        applyColorToElement(gfx, ELEMENT_COLORS.active.stroke, ELEMENT_COLORS.active.fill, ELEMENT_COLORS.active.strokeWidth);
    }
    
    // Add tooltip with execution information
    addTooltipToElement(elementRegistry, elementId, {
        title: `Active: ${elementId}`,
        content: `Execution ID: ${trace.executionId}<br>
                  State: ${trace.state}<br>
                  Executable: ${trace.isExecutable}`
    });
}

/**
 * Apply color to a BPMN element
 * @param {object} gfx - The SVG graphics element
 * @param {string} strokeColor - The stroke color
 * @param {string} fillColor - The fill color
 * @param {number} strokeWidth - The stroke width
 */
function applyColorToElement(gfx, strokeColor, fillColor, strokeWidth) {
    if (!gfx) {
        console.warn('No graphics element provided to applyColorToElement');
        return;
    }
    
    try {
        // Direct styling for SVG elements
        const selectors = ['path', 'rect', 'polygon', 'circle', 'polyline'];
        
        let styled = false;
        selectors.forEach(selector => {
            const elements = gfx.querySelectorAll(selector);
            if (elements.length > 0) {
                styled = true;
                elements.forEach(element => {
                    // Apply stroke color and width
                    if (strokeColor) {
                        element.style.stroke = strokeColor;
                    }
                    
                    if (strokeWidth) {
                        element.style.strokeWidth = strokeWidth + 'px';
                    }
                    
                    // Apply fill color to appropriate elements
                    if (fillColor && (selector === 'rect' || selector === 'circle' || selector === 'polygon')) {
                        element.style.fill = fillColor;
                    }
                });
            }
        });
        
        // If no elements were styled, try direct styling (for some simple elements)
        if (!styled && gfx.tagName && selectors.includes(gfx.tagName.toLowerCase())) {
            if (strokeColor) {
                gfx.style.stroke = strokeColor;
            }
            
            if (strokeWidth) {
                gfx.style.strokeWidth = strokeWidth + 'px';
            }
            
            if (fillColor && (gfx.tagName.toLowerCase() === 'rect' || 
                             gfx.tagName.toLowerCase() === 'circle' || 
                             gfx.tagName.toLowerCase() === 'polygon')) {
                gfx.style.fill = fillColor;
            }
        }
        
        // Add appropriate CSS classes based on colors
        if (strokeColor === ELEMENT_COLORS.active.stroke) {
            gfx.classList.add('active-element');
        } else if (strokeColor === ELEMENT_COLORS.completed.stroke) {
            gfx.classList.add('completed-element');
        } else if (strokeColor === ELEMENT_COLORS.waiting.stroke) {
            gfx.classList.add('waiting-element');
        } else if (strokeColor === ELEMENT_COLORS.error.stroke) {
            gfx.classList.add('error-element');
        }
    } catch (error) {
        console.error('Error applying color to element:', error);
    }
}

/**
 * Add tooltip to a BPMN element
 * @param {object} elementRegistry - The BPMN.js element registry
 * @param {string} elementId - The element ID
 * @param {object} tooltipContent - The tooltip content with title and content properties
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
 * @returns {Promise<string>} The BPMN XML
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
 * @param {string} definitionKey - The definition key
 * @returns {Promise<object>} Result of the save operation
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
 * @param {string} processInstanceId - The process instance ID
 * @param {boolean} autoRefresh - Whether to automatically refresh the execution map
 */
export function updateExecutionMap(processInstanceId, autoRefresh = true) {
    console.log(`Updating execution map for process ${processInstanceId}`);
    initializeViewer(processInstanceId, autoRefresh);
}

/**
 * Get process instance details
 * @param {string} processInstanceId - The process instance ID
 * @returns {Promise<object>} The process instance details
 */
export async function getProcessInstance(processInstanceId) {
    return await fetchProcessInstance(processInstanceId);
}

/**
 * Get all processes
 * @returns {Promise<Array>} List of all processes
 */
export async function getAllProcesses() {
    return await fetchAllProcesses();
}

/**
 * Stop automatic refreshing of execution data
 */
export function stopRefreshing() {
    stopRefreshInterval();
}
