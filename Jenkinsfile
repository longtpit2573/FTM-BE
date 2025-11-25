// Jenkinsfile for FTM Backend - Full CI/CD Pipeline
// 
// Architecture:
// - Runs on Kubernetes agent pod with Docker CLI and kubectl
// - Pod template 'docker-builder' defined in Jenkins Configuration as Code (JCasC)
// - Multi-stage pipeline: Checkout → Docker Build → Push to ACR → Update GitOps
// - Triggers ArgoCD auto-sync for deployment to AKS
//
// Prerequisites:
// - Jenkins with Kubernetes plugin
// - Pod template 'docker-builder' configured with Docker socket mount
// - Credentials: 'acr-credentials', 'git-credentials'

pipeline {
    agent {
        label 'docker-builder'  // Use the pod template we configured
    }

    environment {
        // ACR Configuration
        ACR_NAME = 'acrftmbackenddev'
        ACR_REGISTRY = "${ACR_NAME}.azurecr.io"
        IMAGE_NAME = 'ftm-backend'
        IMAGE_TAG = "v1.0.${env.BUILD_NUMBER}"
        
        // GitOps Configuration
        GITOPS_REPO = 'https://github.com/longtpit2573/Infrastructure.git'
        GITOPS_PATH = 'applications/overlays/dev'
    }
    
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 30, unit: 'MINUTES')
    }
    
    stages {
        stage('📋 Checkout') {
            steps {
                script {
                    echo '========================================='
                    echo '  FTM Backend CI/CD Pipeline'
                    echo '========================================='
                    
                    checkout scm
                    
                    env.GIT_COMMIT_SHORT = sh(
                        script: "git rev-parse --short HEAD",
                        returnStdout: true
                    ).trim()
                    
                    env.GIT_COMMIT_MSG = sh(
                        script: 'git log -1 --pretty=%B',
                        returnStdout: true
                    ).trim()
                    
                    echo "Git Commit: ${env.GIT_COMMIT_SHORT}"
                    echo "Message: ${env.GIT_COMMIT_MSG}"
                    echo "Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
                    echo '========================================='
                }
            }
        }
        
        stage('🐳 Build & Push with Kaniko') {
            steps {
                container('kaniko') {
                    script {
                        echo 'Building image with Kaniko...'
                        
                        // Get ACR credentials and create Docker config
                        withCredentials([usernamePassword(
                            credentialsId: 'acr-credentials',
                            usernameVariable: 'ACR_USER',
                            passwordVariable: 'ACR_PASS'
                        )]) {
                            sh """
                                echo "Creating Docker config for ACR authentication..."
                                
                                # Create Docker config.json for Kaniko
                                mkdir -p /kaniko/.docker
                                
                                cat > /kaniko/.docker/config.json <<EOF
{
  "auths": {
    "${ACR_REGISTRY}": {
      "auth": "\$(echo -n "\${ACR_USER}:\${ACR_PASS}" | base64)"
    }
  }
}
EOF
                                
                                echo "Docker config created successfully"
                            """
                        }
                        
                        // Build and push with Kaniko
                        dir('FTM-BE') {
                            sh """
                                echo "Building and pushing image with Kaniko..."
                                echo "Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
                                echo "Context: \$(pwd)"
                                
                                /kaniko/executor \\
                                  --context=. \\
                                  --dockerfile=Dockerfile \\
                                  --destination=${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} \\
                                  --destination=${ACR_REGISTRY}/${IMAGE_NAME}:latest \\
                                  --cache=true \\
                                  --cache-ttl=24h \\
                                  --compressed-caching=false \\
                                  --snapshot-mode=redo \\
                                  --log-format=text \\
                                  --verbosity=info
                                
                                echo "✅ Image built and pushed successfully with Kaniko"
                            """
                        }
                    }
                }
            }
        }
        
        stage('📝 Update GitOps Repository') {
            steps {
                script {
                    echo 'Updating GitOps repository with new image tag...'
                    
                    // Use default container (jnlp) which has git available
                    withCredentials([usernamePassword(
                        credentialsId: 'git-credentials', 
                        usernameVariable: 'GIT_USER', 
                        passwordVariable: 'GIT_PASS'
                    )]) {
                        sh '''
                            set -e
                            
                            echo "Installing kustomize..."
                            # Install kustomize if not exists
                            if [ ! -f ./kustomize ]; then
                                curl -s "https://raw.githubusercontent.com/kubernetes-sigs/kustomize/master/hack/install_kustomize.sh" | bash
                            fi
                            
                            # Make it executable and save absolute path
                            chmod +x ./kustomize
                            KUSTOMIZE_PATH=$(pwd)/kustomize
                            echo "Kustomize path: ${KUSTOMIZE_PATH}"
                            
                            # Verify kustomize works
                            ${KUSTOMIZE_PATH} version
                            
                            echo "Cloning GitOps repository..."
                            # Clone GitOps repo
                            rm -rf gitops
                            git clone https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git gitops
                            
                            cd gitops/${GITOPS_PATH}
                            
                            echo "Current kustomization.yaml:"
                            cat kustomization.yaml
                            
                            echo ""
                            echo "Updating image tag..."
                            # Update image tag
                            ${KUSTOMIZE_PATH} edit set image ${ACR_REGISTRY}/${IMAGE_NAME}=${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                            
                            echo ""
                            echo "Changes to kustomization.yaml:"
                            git diff kustomization.yaml
                            
                            # Configure git
                            git config user.name "Jenkins CI"
                            git config user.email "jenkins@longops.io.vn"
                            
                            # Stage changes
                            git add kustomization.yaml
                            
                            # Commit with skip ci to avoid triggering GitOps pipelines
                            if git diff --staged --quiet; then
                                echo "No changes to commit"
                            else
                                git commit -m "chore: update backend image to ${IMAGE_TAG} [skip ci]"
                                
                                echo "Pushing changes to GitOps repository..."
                                git push https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git main
                                
                                echo "✅ GitOps repository updated successfully"
                            fi
                        '''
                    }
                    
                    echo '✅ GitOps repo updated'
                    echo '⏳ ArgoCD will auto-sync in ~3 minutes'
                }
            }
        }
    }
    
    post {
        success {
            script {
                echo '========================================='
                echo '  ✅ CI/CD PIPELINE SUCCESS'
                echo '========================================='
                echo "Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
                echo "Commit: ${env.GIT_COMMIT_SHORT}"
                echo "Message: ${env.GIT_COMMIT_MSG}"
                echo ''
                echo 'Next steps:'
                echo '1. ✅ Docker image built and pushed to ACR'
                echo '2. ✅ GitOps repo updated with new image tag'
                echo '3. ⏳ ArgoCD will detect change in ~3 minutes'
                echo '4. ⏳ New pods will be deployed to AKS'
                echo ''
                echo "View deployment:"
                echo "  kubectl get pods -n ftm-production -w"
                echo ''
                echo "View ArgoCD app:"
                echo "  kubectl get application ftm-backend -n argocd"
                echo '========================================='
            }
        }
        failure {
            script {
                echo '========================================='
                echo '  ❌ PIPELINE FAILED'
                echo '========================================='
                echo "Build: ${env.BUILD_NUMBER}"
                echo "Commit: ${env.GIT_COMMIT_SHORT}"
                echo "Check logs: ${env.BUILD_URL}console"
                echo ''
                echo 'Common issues:'
                echo '1. Check Docker daemon is running in pod'
                echo '2. Verify ACR credentials are correct'
                echo '3. Ensure Git credentials have push access'
                echo '4. Check network connectivity to ACR and GitHub'
                echo '========================================='
            }
        }
        cleanup {
            script {
                echo 'Cleaning up workspace...'
                // Clean up gitops clone
                sh 'rm -rf gitops kustomize || true'
            }
        }
    }
}
