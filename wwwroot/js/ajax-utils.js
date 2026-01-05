/**
 * AJAX Utilities for Mess Management System
 * Provides helper functions for API calls, toast notifications, and form handling
 */

const MMS = {
    // Base configuration
    config: {
        baseUrl: window.location.origin,
        defaultHeaders: {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        }
    },

    /**
     * Get anti-forgery token from the page
     */
    getAntiForgeryToken: function() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    },

    /**
     * Generic API call using Fetch
     * @param {string} url - API endpoint
     * @param {object} options - Fetch options
     * @returns {Promise} - Response promise
     */
    api: async function(url, options = {}) {
        const defaultOptions = {
            method: 'GET',
            headers: { ...this.config.defaultHeaders },
            credentials: 'same-origin'
        };

        // Add anti-forgery token for non-GET requests
        if (options.method && options.method !== 'GET') {
            defaultOptions.headers['RequestVerificationToken'] = this.getAntiForgeryToken();
        }

        const finalOptions = { ...defaultOptions, ...options };
        
        if (finalOptions.body && typeof finalOptions.body === 'object' && !(finalOptions.body instanceof FormData)) {
            finalOptions.body = JSON.stringify(finalOptions.body);
        }

        try {
            MMS.showLoading();
            const response = await fetch(url, finalOptions);
            MMS.hideLoading();

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ message: 'An error occurred' }));
                throw new Error(errorData.message || `HTTP Error: ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            MMS.hideLoading();
            throw error;
        }
    },

    /**
     * GET request helper
     */
    get: function(url) {
        return this.api(url, { method: 'GET' });
    },

    /**
     * POST request helper
     */
    post: function(url, data) {
        return this.api(url, { method: 'POST', body: data });
    },

    /**
     * PUT request helper
     */
    put: function(url, data) {
        return this.api(url, { method: 'PUT', body: data });
    },

    /**
     * DELETE request helper
     */
    delete: function(url) {
        return this.api(url, { method: 'DELETE' });
    },

    /**
     * Submit form via AJAX
     * @param {HTMLFormElement} form - The form element
     * @param {object} options - Additional options
     */
    submitForm: async function(form, options = {}) {
        const url = options.url || form.action;
        const method = options.method || form.method || 'POST';
        
        // Validate form first
        if (!form.checkValidity()) {
            form.reportValidity();
            return { success: false, message: 'Please fill in all required fields correctly.' };
        }

        const formData = new FormData(form);
        const data = {};
        
        formData.forEach((value, key) => {
            // Handle multiple values with same key (checkboxes)
            if (data[key]) {
                if (Array.isArray(data[key])) {
                    data[key].push(value);
                } else {
                    data[key] = [data[key], value];
                }
            } else {
                data[key] = value;
            }
        });

        try {
            const result = await this.api(url, { method, body: data });
            
            if (result.success) {
                if (options.successMessage || result.message) {
                    MMS.toast.success(options.successMessage || result.message);
                }
                if (options.onSuccess) {
                    options.onSuccess(result);
                }
                if (options.redirect) {
                    setTimeout(() => window.location.href = options.redirect, 1000);
                }
                if (options.reload) {
                    setTimeout(() => window.location.reload(), 1000);
                }
            } else {
                MMS.toast.error(result.message || 'Operation failed');
                if (options.onError) {
                    options.onError(result);
                }
            }
            
            return result;
        } catch (error) {
            MMS.toast.error(error.message || 'An error occurred');
            if (options.onError) {
                options.onError({ success: false, message: error.message });
            }
            return { success: false, message: error.message };
        }
    },

    /**
     * Toast Notification System
     */
    toast: {
        container: null,

        init: function() {
            if (!this.container) {
                this.container = document.createElement('div');
                this.container.id = 'toast-container';
                this.container.className = 'toast-container position-fixed top-0 end-0 p-3';
                this.container.style.zIndex = '9999';
                document.body.appendChild(this.container);
            }
        },

        show: function(message, type = 'info', duration = 5000) {
            this.init();

            const icons = {
                success: 'fa-check-circle',
                error: 'fa-exclamation-circle',
                warning: 'fa-exclamation-triangle',
                info: 'fa-info-circle'
            };

            const bgColors = {
                success: 'bg-success',
                error: 'bg-danger',
                warning: 'bg-warning',
                info: 'bg-info'
            };

            const toastId = 'toast-' + Date.now();
            const toastHtml = `
                <div id="${toastId}" class="toast align-items-center text-white ${bgColors[type]} border-0" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="${duration}">
                    <div class="d-flex">
                        <div class="toast-body">
                            <i class="fas ${icons[type]} me-2"></i>
                            ${message}
                        </div>
                        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;

            this.container.insertAdjacentHTML('beforeend', toastHtml);
            const toastElement = document.getElementById(toastId);
            const toast = new bootstrap.Toast(toastElement);
            toast.show();

            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });
        },

        success: function(message, duration) {
            this.show(message, 'success', duration);
        },

        error: function(message, duration) {
            this.show(message, 'error', duration);
        },

        warning: function(message, duration) {
            this.show(message, 'warning', duration);
        },

        info: function(message, duration) {
            this.show(message, 'info', duration);
        }
    },

    /**
     * Loading spinner
     */
    loadingOverlay: null,

    showLoading: function(message = 'Processing...') {
        if (!this.loadingOverlay) {
            this.loadingOverlay = document.createElement('div');
            this.loadingOverlay.id = 'loading-overlay';
            this.loadingOverlay.innerHTML = `
                <div class="loading-content">
                    <div class="spinner-border text-light" role="status" style="width: 3rem; height: 3rem;">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="loading-message text-light mt-3 mb-0">${message}</p>
                </div>
            `;
            this.loadingOverlay.style.cssText = `
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(0, 0, 0, 0.7);
                display: flex;
                justify-content: center;
                align-items: center;
                z-index: 10000;
                opacity: 0;
                transition: opacity 0.3s ease;
            `;
            document.body.appendChild(this.loadingOverlay);
        }
        
        this.loadingOverlay.querySelector('.loading-message').textContent = message;
        this.loadingOverlay.style.display = 'flex';
        setTimeout(() => this.loadingOverlay.style.opacity = '1', 10);
    },

    hideLoading: function() {
        if (this.loadingOverlay) {
            this.loadingOverlay.style.opacity = '0';
            setTimeout(() => {
                if (this.loadingOverlay) {
                    this.loadingOverlay.style.display = 'none';
                }
            }, 300);
        }
    },

    /**
     * Confirmation dialog
     */
    confirm: function(message, title = 'Confirm Action') {
        return new Promise((resolve) => {
            const modalId = 'confirmModal-' + Date.now();
            const modalHtml = `
                <div class="modal fade" id="${modalId}" tabindex="-1" data-bs-backdrop="static">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header bg-warning text-dark">
                                <h5 class="modal-title"><i class="fas fa-question-circle me-2"></i>${title}</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <p class="mb-0">${message}</p>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                    <i class="fas fa-times me-1"></i>Cancel
                                </button>
                                <button type="button" class="btn btn-danger" id="${modalId}-confirm">
                                    <i class="fas fa-check me-1"></i>Confirm
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            document.body.insertAdjacentHTML('beforeend', modalHtml);
            const modalElement = document.getElementById(modalId);
            const modal = new bootstrap.Modal(modalElement);

            document.getElementById(`${modalId}-confirm`).addEventListener('click', () => {
                modal.hide();
                resolve(true);
            });

            modalElement.addEventListener('hidden.bs.modal', () => {
                modalElement.remove();
                resolve(false);
            });

            modal.show();
        });
    },

    /**
     * Real-time form validation
     */
    initFormValidation: function(formSelector) {
        const form = document.querySelector(formSelector);
        if (!form) return;

        const inputs = form.querySelectorAll('input, select, textarea');
        
        inputs.forEach(input => {
            input.addEventListener('blur', function() {
                MMS.validateField(this);
            });

            input.addEventListener('input', function() {
                // Remove error on typing
                this.classList.remove('is-invalid');
                const feedback = this.parentElement.querySelector('.invalid-feedback');
                if (feedback) feedback.remove();
            });
        });

        form.addEventListener('submit', function(e) {
            let isValid = true;
            inputs.forEach(input => {
                if (!MMS.validateField(input)) {
                    isValid = false;
                }
            });

            if (!isValid) {
                e.preventDefault();
                MMS.toast.error('Please fix the validation errors before submitting.');
            }
        });
    },

    /**
     * Validate individual field
     */
    validateField: function(input) {
        let isValid = true;
        let message = '';

        // Remove existing feedback
        input.classList.remove('is-valid', 'is-invalid');
        const existingFeedback = input.parentElement.querySelector('.invalid-feedback');
        if (existingFeedback) existingFeedback.remove();

        // Required validation
        if (input.hasAttribute('required') && !input.value.trim()) {
            isValid = false;
            message = 'This field is required.';
        }

        // Email validation
        if (input.type === 'email' && input.value) {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(input.value)) {
                isValid = false;
                message = 'Please enter a valid email address.';
            }
        }

        // Min length validation
        if (input.minLength > 0 && input.value.length < input.minLength) {
            isValid = false;
            message = `Minimum ${input.minLength} characters required.`;
        }

        // Pattern validation
        if (input.pattern && input.value) {
            const pattern = new RegExp(input.pattern);
            if (!pattern.test(input.value)) {
                isValid = false;
                message = input.title || 'Please match the requested format.';
            }
        }

        // Phone validation
        if (input.type === 'tel' && input.value) {
            const phoneRegex = /^[0-9]{10,15}$/;
            if (!phoneRegex.test(input.value.replace(/\D/g, ''))) {
                isValid = false;
                message = 'Please enter a valid phone number.';
            }
        }

        // Apply validation state
        if (isValid) {
            input.classList.add('is-valid');
        } else {
            input.classList.add('is-invalid');
            const feedback = document.createElement('div');
            feedback.className = 'invalid-feedback';
            feedback.textContent = message;
            input.parentElement.appendChild(feedback);
        }

        return isValid;
    },

    /**
     * Delete with confirmation
     */
    deleteWithConfirm: async function(url, options = {}) {
        const message = options.message || 'Are you sure you want to delete this item?';
        const title = options.title || 'Confirm Delete';

        const confirmed = await this.confirm(message, title);
        if (!confirmed) return { success: false, cancelled: true };

        try {
            const result = await this.delete(url);
            if (result.success) {
                MMS.toast.success(options.successMessage || 'Item deleted successfully.');
                if (options.onSuccess) options.onSuccess(result);
                if (options.removeElement) {
                    const element = document.querySelector(options.removeElement);
                    if (element) {
                        element.style.transition = 'opacity 0.3s, transform 0.3s';
                        element.style.opacity = '0';
                        element.style.transform = 'translateX(-20px)';
                        setTimeout(() => element.remove(), 300);
                    }
                }
            } else {
                MMS.toast.error(result.message || 'Failed to delete item.');
            }
            return result;
        } catch (error) {
            MMS.toast.error(error.message || 'An error occurred while deleting.');
            return { success: false, message: error.message };
        }
    },

    /**
     * Refresh table/list data via AJAX
     */
    refreshData: async function(url, containerId, options = {}) {
        try {
            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            
            if (response.ok) {
                const html = await response.text();
                const container = document.getElementById(containerId);
                if (container) {
                    container.innerHTML = html;
                    if (options.onSuccess) options.onSuccess();
                }
            }
        } catch (error) {
            console.error('Error refreshing data:', error);
            if (options.onError) options.onError(error);
        }
    },

    /**
     * Initialize AJAX forms on page
     */
    initAjaxForms: function() {
        document.querySelectorAll('form[data-ajax="true"]').forEach(form => {
            form.addEventListener('submit', async function(e) {
                e.preventDefault();
                
                const successMessage = this.dataset.successMessage;
                const redirect = this.dataset.redirect;
                const reload = this.dataset.reload === 'true';

                await MMS.submitForm(this, {
                    successMessage,
                    redirect,
                    reload
                });
            });
        });
    }
};

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    MMS.initAjaxForms();
});
