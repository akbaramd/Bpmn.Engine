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

// Execution state enum (matching backend C# enum)
const EXECUTION_STATE = {
    ACTIVE: 0,
    PAUSED: 1,
    COMPLETED: 2,
    TERMINATED: 3,
    FAILED: 4,
    DEACTIVE: 5
};

// Material Design color palette - A500 values
const MATERIAL_COLORS = {
    // Primary colors
    green: { main: '#4CAF50', light: '#81C784', dark: '#388E3C', accent: '#00E676' },
    blue: { main: '#2196F3', light: '#64B5F6', dark: '#1976D2', accent: '#448AFF' },
    purple: { main: '#9C27B0', light: '#BA68C8', dark: '#7B1FA2', accent: '#E040FB' },
    orange: { main: '#FF9800', light: '#FFB74D', dark: '#F57C00', accent: '#FFAB40' },
    red: { main: '#F44336', light: '#E57373', dark: '#D32F2F', accent: '#FF5252' },
    teal: { main: '#009688', light: '#4DB6AC', dark: '#00796B', accent: '#64FFDA' },
    indigo: { main: '#3F51B5', light: '#7986CB', dark: '#303F9F', accent: '#536DFE' },
    cyan: { main: '#00BCD4', light: '#4DD0E1', dark: '#0097A7', accent: '#18FFFF' },
    amber: { main: '#FFC107', light: '#FFD54F', dark: '#FFA000', accent: '#FFD740' },
    brown: { main: '#795548', light: '#A1887F', dark: '#5D4037', accent: '#8D6E63' },
    grey: { main: '#9E9E9E', light: '#E0E0E0', dark: '#616161', accent: '#BDBDBD' },
    blueGrey: { main: '#607D8B', light: '#90A4AE', dark: '#455A64', accent: '#78909C' }
};

// Element state colors - using Material Design palette
const ELEMENT_COLORS = {
    // State-based colors
    active: { stroke: MATERIAL_COLORS.green.main, strokeWidth: 3, textColor: MATERIAL_COLORS.green.main },
    paused: { stroke: MATERIAL_COLORS.orange.main, strokeWidth: 2, textColor: MATERIAL_COLORS.orange.main },
    completed: { stroke: MATERIAL_COLORS.grey.main, strokeWidth: 2, textColor: MATERIAL_COLORS.grey.main },
    terminated: { stroke: MATERIAL_COLORS.brown.main, strokeWidth: 2, textColor: MATERIAL_COLORS.brown.main },
    failed: { stroke: MATERIAL_COLORS.red.main, strokeWidth: 2, textColor: MATERIAL_COLORS.red.main },
    deactive: { stroke: MATERIAL_COLORS.grey.light, strokeWidth: 1, textColor: MATERIAL_COLORS.grey.main },
    
    // Executable status colors
    executable: { stroke: MATERIAL_COLORS.green.accent, strokeWidth: 3, textColor: MATERIAL_COLORS.green.accent },
    nonExecutable: { stroke: MATERIAL_COLORS.grey.main, strokeWidth: 2, textColor: MATERIAL_COLORS.grey.main },
    
    // Default
    default: { stroke: MATERIAL_COLORS.blueGrey.light, strokeWidth: 1, textColor: '#333333' }
};

// Flow-specific colors (brighter for better visibility)
const FLOW_COLORS = {
    // State-based colors
    active: { stroke: MATERIAL_COLORS.green.accent, strokeWidth: 4 },
    paused: { stroke: MATERIAL_COLORS.orange.accent, strokeWidth: 3 },
    completed: { stroke: MATERIAL_COLORS.grey.accent, strokeWidth: 3 },
    terminated: { stroke: MATERIAL_COLORS.brown.accent, strokeWidth: 3 },
    failed: { stroke: MATERIAL_COLORS.red.accent, strokeWidth: 3 },
    deactive: { stroke: MATERIAL_COLORS.grey.light, strokeWidth: 2 },
    
    // Executable status colors
    executable: { stroke: MATERIAL_COLORS.green.accent, strokeWidth: 4 },
    nonExecutable: { stroke: MATERIAL_COLORS.grey.accent, strokeWidth: 3 },
    
    // Default
    default: { stroke: MATERIAL_COLORS.blueGrey.light, strokeWidth: 2 }
};

// Element type styles - using Material Design palette
const ELEMENT_TYPE_STYLES = {
    // Events
    'bpmn:StartEvent': { stroke: MATERIAL_COLORS.green.dark, strokeWidth: 2 },
    'bpmn:EndEvent': { stroke: MATERIAL_COLORS.red.dark, strokeWidth: 2 },
    'bpmn:IntermediateThrowEvent': { stroke: MATERIAL_COLORS.orange.dark, strokeWidth: 2 },
    'bpmn:IntermediateCatchEvent': { stroke: MATERIAL_COLORS.orange.dark, strokeWidth: 2 },
    'bpmn:BoundaryEvent': { stroke: MATERIAL_COLORS.purple.dark, strokeWidth: 2 },
    
    // Tasks
    'bpmn:Task': { stroke: MATERIAL_COLORS.blue.dark, strokeWidth: 2 },
    'bpmn:UserTask': { stroke: MATERIAL_COLORS.blue.main, strokeWidth: 2 },
    'bpmn:ServiceTask': { stroke: MATERIAL_COLORS.purple.dark, strokeWidth: 2 },
    'bpmn:ScriptTask': { stroke: MATERIAL_COLORS.teal.dark, strokeWidth: 2 },
    'bpmn:BusinessRuleTask': { stroke: MATERIAL_COLORS.orange.dark, strokeWidth: 2 },
    'bpmn:ManualTask': { stroke: MATERIAL_COLORS.brown.main, strokeWidth: 2 },
    
    // Gateways
    'bpmn:ExclusiveGateway': { stroke: MATERIAL_COLORS.amber.dark, strokeWidth: 2 },
    'bpmn:ParallelGateway': { stroke: MATERIAL_COLORS.teal.dark, strokeWidth: 2 },
    'bpmn:InclusiveGateway': { stroke: MATERIAL_COLORS.purple.light, strokeWidth: 2 },
    'bpmn:Gateway': { stroke: MATERIAL_COLORS.amber.dark, strokeWidth: 2 },
    
    // Flows - using the same colors as executable/non-executable for consistency
    'bpmn:SequenceFlow': { stroke: MATERIAL_COLORS.green.accent, strokeWidth: 2 },
    'bpmn:MessageFlow': { stroke: MATERIAL_COLORS.brown.dark, strokeWidth: 2 },
    'bpmn:Association': { stroke: MATERIAL_COLORS.blueGrey.accent, strokeWidth: 1 },
    
    // Other elements
    'bpmn:FlowElement': { stroke: MATERIAL_COLORS.blueGrey.accent, strokeWidth: 2 },
    'bpmn:FlowNode': { stroke: MATERIAL_COLORS.blueGrey.accent, strokeWidth: 2 },
    'bpmn:DataObject': { stroke: MATERIAL_COLORS.indigo.light, strokeWidth: 2 },
    'bpmn:DataStore': { stroke: MATERIAL_COLORS.indigo.light, strokeWidth: 2 },
    'bpmn:TextAnnotation': { stroke: MATERIAL_COLORS.blueGrey.main, strokeWidth: 1 }
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
    
    // Track non-executable elements to mark their outgoing flows as non-executable
    const nonExecutableElements = new Set();
    const executableElements = new Set();
    
    // Process execution traces
    if (executionMap && executionMap.traces && Array.isArray(executionMap.traces)) {
        console.log('Processing execution traces:', executionMap.traces);
        
        // First pass: Identify executable and non-executable elements
        executionMap.traces.forEach(trace => {
            if (trace.currentElementId) {
                if (trace.isExecutable) {
                    executableElements.add(trace.currentElementId);
                } else {
                    nonExecutableElements.add(trace.currentElementId);
                }
            }
        });
        
        // Second pass: Mark all completed elements
        executionMap.traces.forEach(trace => {
            if (trace.path && Array.isArray(trace.path)) {
                trace.path.forEach(elementId => {
                    const element = elementRegistry.get(elementId);
                    if (element) {
                        // Mark as completed unless it's the current element
                        if (!(elementId === trace.currentElementId)) {
                            // Only mark as completed if the trace is executable
                            if (trace.isExecutable) {
                                markElementAsCompleted(canvas, elementRegistry, element, elementId, trace);
                            }
                        }
                    }
                });
            }
        });
        
        // Third pass: Mark current elements with their current state
        executionMap.traces.forEach(trace => {
            if (trace.currentElementId) {
                const element = elementRegistry.get(trace.currentElementId);
                if (element) {
                    // Pass the trace object which includes isExecutable property and state
                    markElementAsActive(canvas, elementRegistry, element, trace.currentElementId, trace);
                }
            }
        });
        
        // Fourth pass: Process flow connections
        elementRegistry.forEach(element => {
            // Only process sequence flows
            if (isSequenceFlow(element)) {
                const sourceId = element.source?.id;
                const targetId = element.target?.id;
                
                // Check if source or target is non-executable
                if (sourceId && nonExecutableElements.has(sourceId)) {
                    // Flow from non-executable element is always non-executable
                    markFlowAsNonExecutable(canvas, elementRegistry, element, element.id);
                } else if (targetId && nonExecutableElements.has(targetId)) {
                    // Flow to non-executable element is always non-executable
                    markFlowAsNonExecutable(canvas, elementRegistry, element, element.id);
                }
            }
        });
        
        // Fifth pass: Check for flows between elements and mark them appropriately
        elementRegistry.forEach(element => {
            if (isSequenceFlow(element)) {
                const sourceId = element.source?.id;
                const targetId = element.target?.id;
                
                // If we have both source and target
                if (sourceId && targetId) {
                    // If either source or target is in the non-executable set, mark flow as non-executable
                    if (nonExecutableElements.has(sourceId) || nonExecutableElements.has(targetId)) {
                        markFlowAsNonExecutable(canvas, elementRegistry, element, element.id);
                    }
                    // If both source and target are in the executable set, mark flow as executable
                    else if (executableElements.has(sourceId) && executableElements.has(targetId)) {
                        // Mark as executable
                        canvas.addMarker(element.id, 'executable');
                        canvas.addMarker(element.id, 'executable-flow');
                        const gfx = elementRegistry.getGraphics(element.id);
                        if (gfx) {
                            applyColorToElement(gfx, ELEMENT_COLORS.executable.stroke, ELEMENT_COLORS.executable.strokeWidth);
                        }
                    }
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
                    // Convert string state to numeric if needed
                    let stateValue;
                    if (typeof context.state === 'string') {
                        // Map string state to numeric value
                        switch (context.state.toLowerCase()) {
                            case 'active': stateValue = EXECUTION_STATE.ACTIVE; break;
                            case 'paused': stateValue = EXECUTION_STATE.PAUSED; break;
                            case 'completed': stateValue = EXECUTION_STATE.COMPLETED; break;
                            case 'terminated': stateValue = EXECUTION_STATE.TERMINATED; break;
                            case 'failed': stateValue = EXECUTION_STATE.FAILED; break;
                            case 'deactive': stateValue = EXECUTION_STATE.DEACTIVE; break;
                            default: stateValue = EXECUTION_STATE.ACTIVE;
                        }
                    } else {
                        // Already numeric
                        stateValue = context.state;
                    }
                    
                    // Check if the context is executable
                    const isExecutable = (stateValue === EXECUTION_STATE.ACTIVE);
                    
                    markElementAsActive(canvas, elementRegistry, element, context.currentElementId, {
                        executionId: context.contextId,
                        state: stateValue,
                        isExecutable: isExecutable
                    });
                    
                    // If not executable, add to non-executable set for flow processing
                    if (!isExecutable) {
                        nonExecutableElements.add(context.currentElementId);
                    }
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
 * @param {object} trace - Optional trace data
 */
function markElementAsCompleted(canvas, elementRegistry, element, elementId, trace) {
    canvas.addMarker(elementId, 'completed');
    const gfx = elementRegistry.getGraphics(elementId);
    if (!gfx) return;
    
    // Check if this is an executable completed element
    const isExecutable = trace ? trace.isExecutable : true;
    
    // Apply different styles based on element type and executable status
    if (isSequenceFlow(element)) {
        // For sequence flows
        if (isExecutable) {
            applyColorToElement(gfx, FLOW_COLORS.executable.stroke, FLOW_COLORS.executable.strokeWidth);
        } else {
            applyColorToElement(gfx, FLOW_COLORS.nonExecutable.stroke, FLOW_COLORS.nonExecutable.strokeWidth);
        }
    } else {
        // For other elements
        if (isExecutable) {
            applyColorToElement(gfx, ELEMENT_COLORS.executable.stroke, ELEMENT_COLORS.executable.strokeWidth);
        } else {
            applyColorToElement(gfx, ELEMENT_COLORS.nonExecutable.stroke, ELEMENT_COLORS.nonExecutable.strokeWidth);
        }
    }
    
    // Add tooltip with execution information
    addTooltipToElement(elementRegistry, elementId, {
        title: `Completed: ${elementId}`,
        content: `Element has been executed${trace ? `<br>Executable: ${isExecutable ? 'Yes' : 'No'}` : ''}`
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
    const gfx = elementRegistry.getGraphics(elementId);
    if (!gfx) return;
    
    // Get the state name from the numeric state value
    const stateName = getStateName(trace.state);
    
    // Add appropriate marker based on executable status
    if (trace.isExecutable) {
        canvas.addMarker(elementId, 'executable');
    } else {
        canvas.addMarker(elementId, 'non-executable');
    }
    
    // Add marker for state
    canvas.addMarker(elementId, stateName);
    
    // Apply different styles based on element type and executable status
    if (isSequenceFlow(element)) {
        // For sequence flows
        if (trace.isExecutable) {
            applyColorToElement(gfx, FLOW_COLORS.executable.stroke, FLOW_COLORS.executable.strokeWidth);
        } else {
            applyColorToElement(gfx, FLOW_COLORS.nonExecutable.stroke, FLOW_COLORS.nonExecutable.strokeWidth);
        }
    } else {
        // For other elements
        if (trace.isExecutable) {
            applyColorToElement(gfx, ELEMENT_COLORS.executable.stroke, ELEMENT_COLORS.executable.strokeWidth);
        } else {
            applyColorToElement(gfx, ELEMENT_COLORS.nonExecutable.stroke, ELEMENT_COLORS.nonExecutable.strokeWidth);
        }
    }
    
    // Add tooltip with execution information
    const stateText = getStateName(trace.state).charAt(0).toUpperCase() + getStateName(trace.state).slice(1);
    addTooltipToElement(elementRegistry, elementId, {
        title: `${stateText}: ${elementId}`,
        content: `Execution ID: ${trace.executionId}<br>
                  State: ${stateText}<br>
                  Executable: ${trace.isExecutable ? 'Yes' : 'No'}`
    });
}

/**
 * Apply color to a BPMN element
 * @param {object} gfx - The SVG graphics element
 * @param {string} strokeColor - The stroke color
 * @param {number} strokeWidth - The stroke width
 */
function applyColorToElement(gfx, strokeColor, strokeWidth) {
    if (!gfx) {
        console.warn('No graphics element provided to applyColorToElement');
        return;
    }
    
    try {
        // Determine if this is a flow/connection element
        const isFlow = gfx.classList.contains('djs-connection') || 
                      gfx.parentElement?.classList.contains('djs-connection') ||
                      (gfx.getAttribute('data-element-id') && 
                       (gfx.getAttribute('data-element-id').includes('Flow') || 
                        gfx.getAttribute('data-element-id').includes('Connection')));
        
        // Get the textColor based on the strokeColor
        let textColor = strokeColor;
        Object.keys(ELEMENT_COLORS).forEach(key => {
            if (strokeColor === ELEMENT_COLORS[key].stroke && ELEMENT_COLORS[key].textColor) {
                textColor = ELEMENT_COLORS[key].textColor;
            }
        });
        
        // Apply styles to stroke elements (not text)
        const strokeElements = [
            'path', 'rect', 'polygon', 'circle', 'polyline', 'line', 'ellipse', 
            'g', 'use', 'marker'
        ];
        
        // Apply stroke styles to non-text elements
        strokeElements.forEach(selector => {
            const elements = gfx.querySelectorAll(selector);
            elements.forEach(element => {
                if (strokeColor) {
                    element.style.stroke = strokeColor;
                }
                if (strokeWidth) {
                    element.style.strokeWidth = strokeWidth + 'px';
                }
            });
        });
        
        // Special handling for text elements - only change fill color, not stroke
        const textElements = gfx.querySelectorAll('text, tspan');
        textElements.forEach(textElement => {
            textElement.style.stroke = 'none'; // No stroke for text
            textElement.style.fill = textColor; // Use text color for fill
        });
        
        // Enhanced handling for flow lines (connections)
        if (isFlow) {
            // Apply special flow styling
            const allPaths = gfx.querySelectorAll('path, polyline, line');
            allPaths.forEach(path => {
                if (strokeColor) {
                    path.style.stroke = strokeColor;
                }
                if (strokeWidth) {
                    // Increase stroke width for better visibility
                    path.style.strokeWidth = (strokeWidth + 1) + 'px';
                }
                
                // Add dashed effect for non-executable flows
                if (strokeColor === ELEMENT_COLORS.nonExecutable.stroke) {
                    path.style.strokeDasharray = '5,3';
                } else {
                    path.style.strokeDasharray = 'none';
                }
            });
            
            // Handle markers (arrowheads) for flow direction
            const markers = gfx.querySelectorAll('marker');
            markers.forEach(marker => {
                if (strokeColor) {
                    marker.style.stroke = strokeColor;
                    
                    // Also color the marker's path
                    const markerPaths = marker.querySelectorAll('path');
                    markerPaths.forEach(markerPath => {
                        markerPath.style.stroke = strokeColor;
                        markerPath.style.fill = strokeColor;
                    });
                }
            });
            
            // Apply color to all defs elements
            const defs = gfx.querySelectorAll('defs path');
            defs.forEach(defPath => {
                if (strokeColor) {
                    defPath.style.stroke = strokeColor;
                    defPath.style.fill = strokeColor;
                }
            });
            
            // Style the parent if it's a connection
            if (gfx.parentElement?.classList.contains('djs-connection')) {
                const parentElement = gfx.parentElement;
                parentElement.style.zIndex = '1000'; // Bring flows to front
                
                // Apply stroke color to the parent element itself
                if (strokeColor) {
                    parentElement.style.stroke = strokeColor;
                }
                
                // Apply to all visual elements
                const visualGroup = parentElement.querySelector('.djs-visual');
                if (visualGroup && strokeColor) {
                    visualGroup.style.stroke = strokeColor;
                }
                
                // Apply to all paths in the parent
                const parentPaths = parentElement.querySelectorAll('path, polyline, line');
                parentPaths.forEach(path => {
                    if (strokeColor) {
                        path.style.stroke = strokeColor;
                    }
                    if (strokeWidth) {
                        path.style.strokeWidth = (strokeWidth + 1) + 'px';
                    }
                    
                    // Add dashed effect for non-executable flows
                    if (strokeColor === ELEMENT_COLORS.nonExecutable.stroke) {
                        path.style.strokeDasharray = '5,3';
                    } else {
                        path.style.strokeDasharray = 'none';
                    }
                });
                
                // Apply to all markers in the parent
                const parentMarkers = parentElement.querySelectorAll('marker');
                parentMarkers.forEach(marker => {
                    if (strokeColor) {
                        marker.style.stroke = strokeColor;
                        
                        // Also color the marker's path
                        const markerPaths = marker.querySelectorAll('path');
                        markerPaths.forEach(markerPath => {
                            markerPath.style.stroke = strokeColor;
                            markerPath.style.fill = strokeColor;
                        });
                    }
                });
            }
        }
        
        // If the element itself is an SVG element, style it directly
        if (gfx.tagName) {
            const tagName = gfx.tagName.toLowerCase();
            if (strokeElements.includes(tagName)) {
                if (strokeColor) {
                    gfx.style.stroke = strokeColor;
                }
                if (strokeWidth) {
                    gfx.style.strokeWidth = strokeWidth + 'px';
                }
            } else if (tagName === 'text' || tagName === 'tspan') {
                gfx.style.stroke = 'none';
                gfx.style.fill = textColor;
            }
        }
        
        // Add appropriate CSS classes for additional styling
        if (strokeColor === ELEMENT_COLORS.executable.stroke) {
            gfx.classList.add('executable-element');
            if (isFlow) gfx.classList.add('executable-flow');
        } else if (strokeColor === ELEMENT_COLORS.nonExecutable.stroke) {
            gfx.classList.add('non-executable-element');
            if (isFlow) gfx.classList.add('non-executable-flow');
        } else if (strokeColor === ELEMENT_COLORS.active.stroke) {
            gfx.classList.add('active-element');
            if (isFlow) gfx.classList.add('active-flow');
        } else if (strokeColor === ELEMENT_COLORS.completed.stroke) {
            gfx.classList.add('completed-element');
            if (isFlow) gfx.classList.add('completed-flow');
        } else if (strokeColor === ELEMENT_COLORS.paused.stroke) {
            gfx.classList.add('paused-element');
            if (isFlow) gfx.classList.add('paused-flow');
        } else if (strokeColor === ELEMENT_COLORS.failed.stroke) {
            gfx.classList.add('failed-element');
            if (isFlow) gfx.classList.add('failed-flow');
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
 * Check if element is a sequence flow
 * @param {object} element - The BPMN element
 * @returns {boolean} - True if element is a sequence flow
 */
function isSequenceFlow(element) {
    return element.type === 'bpmn:SequenceFlow' || 
           element.type.includes('Flow') || 
           element.type.includes('Connection') || 
           (element.waypoints && element.waypoints.length > 1);
}

/**
 * Mark a flow as non-executable
 * @param {object} canvas - The BPMN.js canvas
 * @param {object} elementRegistry - The BPMN.js element registry
 * @param {object} element - The BPMN element
 * @param {string} elementId - The element ID
 */
function markFlowAsNonExecutable(canvas, elementRegistry, element, elementId) {
    // Add non-executable marker
    canvas.addMarker(elementId, 'non-executable');
    canvas.addMarker(elementId, 'non-executable-flow');
    
    const gfx = elementRegistry.getGraphics(elementId);
    if (!gfx) return;
    
    // Apply non-executable styling
    applyColorToElement(gfx, ELEMENT_COLORS.nonExecutable.stroke, ELEMENT_COLORS.nonExecutable.strokeWidth);
    
    // Add tooltip
    addTooltipToElement(elementRegistry, elementId, {
        title: `Non-Executable Flow: ${elementId}`,
        content: `This flow is non-executable because it connects to or from a non-executable element.`
    });
}

/**
 * Check if element is a task
 * @param {object} element - The BPMN element
 * @returns {boolean} - True if element is a task
 */
function isTask(element) {
    return element.type.includes('Task');
}

/**
 * Check if element is an event
 * @param {object} element - The BPMN element
 * @returns {boolean} - True if element is an event
 */
function isEvent(element) {
    return element.type.includes('Event');
}

/**
 * Check if element is a gateway
 * @param {object} element - The BPMN element
 * @returns {boolean} - True if element is a gateway
 */
function isGateway(element) {
    return element.type.includes('Gateway');
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
